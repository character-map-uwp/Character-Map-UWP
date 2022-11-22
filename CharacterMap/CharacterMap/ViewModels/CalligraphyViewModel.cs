using CharacterMap.Core;
using CharacterMap.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Media.Imaging;

namespace CharacterMap.ViewModels
{
    public partial class CalligraphyViewModel : ViewModelBase
    {
        [ObservableProperty] private bool _isOverlayVisible = true;

        [ObservableProperty] private string _text;

        [ObservableProperty] private double _fontSize = 220d;

        [ObservableProperty] InkStrokeManager _inkManager = null;

        public FontVariant Face { get; }

        public ObservableCollection<CalligraphyHistoryItem> Histories { get; } = new();

        public CalligraphyViewModel(CharacterRenderingOptions options)
        {
            Face = options.Variant;
        }

        void EnsureManager(InkStrokeContainer container)
        {
            if (_inkManager is null)
                InkManager = new InkStrokeManager(container);
        }

        public async Task AddToHistoryAsync()
        {
            CalligraphyHistoryItem h = await InkManager.CreateHistoryItemAsync(FontSize, Text);
            Histories.Add(h);
        }

        /// <summary>
        /// Clear contents of Ink container and the Redo stack
        /// </summary>
        /// <param name="container"></param>
        public void Clear()
        {
            _inkManager.Clear();
        }

        internal void OnStrokesErased(InkStrokeContainer container, IReadOnlyList<InkStroke> strokes)
        {
            InkManager.OnErased(strokes);
        }

        internal void OnStrokeDrawn(InkStrokeContainer container, IReadOnlyList<InkStroke> strokes)
        {
            EnsureManager(container);
            InkManager.OnDrawn(strokes);
        }

        public async Task SaveAsync(IReadOnlyList<InkStroke> strokes, ExportFormat format, ICanvasResourceCreatorWithDpi device, Rect bounds)
        {
            // 1. Setup and render ink strokes
            bool isPng = format == ExportFormat.Png;
            using CanvasSvgDocument svgDocument = new(device);
            using CanvasRenderTarget target = new CanvasRenderTarget(device, (float)bounds.Width, (float)bounds.Height);
            using (var ds = target.CreateDrawingSession())
            {
                if (isPng)
                    RenderBitmap(strokes, ds, bounds);
                else
                    RenderSVG(strokes, svgDocument, ds, bounds);
            }

            // 2. Pick save file
            string ext = isPng ? "png" : "svg";

            FileSavePicker picker = new ();
            picker.DefaultFileExtension = $".{ext}";
            picker.FileTypeChoices.Add(ext.ToUpper(), new List<String> { $".{ext}" });

            // 3. Write output to save file
            if (await picker.PickSaveFileAsync() is StorageFile file)
            {
                using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

                if (isPng)
                    await target.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                else
                    await svgDocument.SaveAsync(stream);

                // 3.1. If we're overwriting an existing file that was bigger
                //      than our new content, remove the old data.
                stream.Size = stream.Position;

                // 4. Send In-app Notification
                Messenger.Send(new AppNotificationMessage(true, new ExportResult(ExportState.Succeeded, file)));
            }
        }

        private static void RenderBitmap(IReadOnlyList<InkStroke> sourceStrokes, CanvasDrawingSession ds, Rect bounds)
        {
            // We only want to render the drawn parts of the canvas but our strokes are
            // relative to the entire size of the canvas, so we need to move them back.
            
            // 1. Create a translation to move drawing to 0,0
            Matrix3x2 translate = Matrix3x2.CreateTranslation((float)-bounds.X, (float)-bounds.Y);

            // 2. Clone the strokes and apply the translation to each of them
            var strokes = sourceStrokes.Select(s => s.Clone()).ToList();
            foreach (var st in strokes)
                st.PointTransform *= translate;

            // 3. Render the transformed strokes
            ds.Clear(Colors.Transparent);
            ds.DrawInk(strokes);
        }

        private static void RenderSVG(IReadOnlyList<InkStroke> strokes, CanvasSvgDocument svgDocument, CanvasDrawingSession ds, Rect bounds)
        {
            // 1. We only want to render the drawn parts of the canvas but our strokes are
            //    relative to the entire size of the canvas, so set the viewBox to only
            //    the drawn bounds.
            svgDocument.Root.SetRectangleAttribute("viewBox", bounds);

            // 2. Create an SVG path for each stroke
            InkStroke[] s = new InkStroke[1]; 
            foreach (InkStroke stroke in strokes)
            {
                s[0] = stroke;
                SVGPathReciever pathReceiver = new (CanvasGeometry.CreateInk(ds, s));

                CanvasSvgNamedElement element = svgDocument.Root.CreateAndAppendNamedChildElement("path");
                element.SetStringAttribute("d", pathReceiver.GetPathData());
                element.SetColorAttribute("fill", stroke.DrawingAttributes.Color);
            }
        }
    }

    /// <summary>
    /// Helper class to maintain reference between strokes in
    /// Undo/Redo/Container ink strokes, including when cloned.
    /// </summary>
    public class InkStrokeReference
    {
        public InkStroke ActiveStroke { get; private set; }

        public InkStrokeReference(InkStroke stroke)
        {
            ActiveStroke = stroke;
        }

        public InkStroke Clone() => (ActiveStroke = ActiveStroke.Clone());
    }

    [ObservableObject]
    public partial class InkStrokeManager
    {
        [ObservableProperty] private bool _hasStrokes;

        [ObservableProperty] private bool _canUndo;

        [ObservableProperty] private bool _canRedo;

        private Stack<InkActionBase> _redoStack { get; } = new();
        private Stack<InkActionBase> _undoStack { get; } = new();
        private HashSet<InkStrokeReference> _strokeSet { get; } = new HashSet<InkStrokeReference>();

        private InkStrokeContainer _container;

        public InkStrokeManager(InkStrokeContainer container)
        {
            _container = container;
        }

        /// <summary>
        /// Clears the ink canvas, undo & redo stacks and updates UI bindings.
        /// </summary>
        public void Clear()
        {
            _strokeSet.Clear();
            _container.Clear();

            _redoStack.Clear();
            _undoStack.Clear();

            UpdateControls();
        }

        /// <summary>
        /// Gets an existing reference too an InkStroke or creates a new one 
        /// if one doesn't exist
        /// </summary>
        /// <param name="stroke"></param>
        /// <returns></returns>
        public InkStrokeReference GetReference(InkStroke stroke)
        {
            if (FindReference(stroke) is InkStrokeReference r)
                return r;

            r = new InkStrokeReference(stroke);
            _strokeSet.Add(r);
            return r;
        }

        /// <summary>
        /// Finds an existing reference too an InkStroke
        /// </summary>
        /// <param name="stroke"></param>
        /// <returns></returns>
        public InkStrokeReference FindReference(InkStroke stroke)
        {
            return _strokeSet.FirstOrDefault(s => s.ActiveStroke == stroke);
        }

        public void UpdateControls()
        {
            HasStrokes = _container.GetStrokes().Any();
            CanUndo = _undoStack.Count > 0;
            CanRedo = _redoStack.Count > 0;
        }

        internal void OnErased(IReadOnlyList<InkStroke> strokes)
        {
            _undoStack.Push(new StrokeErasedAction(this, strokes));
            _redoStack.Clear();
            UpdateControls();
        }

        internal void OnDrawn(IReadOnlyList<InkStroke> strokes)
        {
            _undoStack.Push(new StrokeDrawnAction(this, strokes));
            CanUndo = true;
            HasStrokes = true;

            // When user draws a stroke manually, clear the redo stack
            _redoStack.Clear();
            CanRedo = false;
        }

        public bool Redo()
        {
            if (CanRedo)
            {
                // 1. Get and execute the most recent redo command
                var command = _redoStack.Pop();
                command.Redo(_container);

                // 2. Push it to the undo stack
                _undoStack.Push(command);

                // 3. Update UI commands
                UpdateControls();
                return true;
            }

            return false;
        }

        public bool Undo()
        {
            if (CanUndo)
            {
                // 1. Get and execute the most recent undo command
                var command = _undoStack.Pop();
                command.Undo(_container);

                // 2. Push it to the redo stack
                _redoStack.Push(command);

                // 3. Update UI commands
                UpdateControls();
                return true;
            }

            return false;
        }

        public async Task<CalligraphyHistoryItem> CreateHistoryItemAsync(double fontSize, string text)
        {
            // 1. Create a History Item
            CalligraphyHistoryItem h = new(_container.GetStrokes(), fontSize, text, _container.BoundingRect);

            // 2. Render a thumbnail of the drawing to a memory stream
            using MemoryStream m = new();
            await _container.SaveAsync(m.AsOutputStream());
            m.Seek(0, SeekOrigin.Begin);

            // 3. Create a XAML BitmapImage from the memory stream
            BitmapImage b = new();
            b.DecodePixelType = DecodePixelType.Logical;
            b.DecodePixelWidth = 150;
            await b.SetSourceAsync(m.AsRandomAccessStream());

            // 4. Add the thumbnail to the History Item
            h.Thumbnail = b;

            return h;
        }
    }

    public abstract class InkActionBase
    {
        public abstract void Undo(InkStrokeContainer container);
        public abstract void Redo(InkStrokeContainer container);
    }

    public class StrokeDrawnAction : InkActionBase
    {
        private readonly List<InkStrokeReference> _strokes;

        public StrokeDrawnAction(InkStrokeManager manager, IEnumerable<InkStroke> strokes)
        {
            _strokes = strokes.Select(s => manager.GetReference(s)).ToList();
        }

        public override void Redo(InkStrokeContainer container)
        {
            foreach (var stroke in _strokes)
                container.AddStroke(stroke.Clone());
        }

        public override void Undo(InkStrokeContainer container)
        {
            // Delete the strokes from the canvas
            foreach (var stroke in _strokes)
            {
                var cStroke = stroke.ActiveStroke;
                cStroke.Selected = true;
                container.DeleteSelected();
                cStroke.Selected = false;
            }
        }
    }

    public class StrokeErasedAction : InkActionBase
    {
        private readonly List<InkStrokeReference> _strokes;

        public StrokeErasedAction(InkStrokeManager manager, IEnumerable<InkStroke> strokes)
        {
            _strokes = strokes.Select(s => manager.GetReference(s)).ToList();
        }

        public override void Redo(InkStrokeContainer container)
        {
            foreach (var stroke in _strokes)
            {
                stroke.ActiveStroke.Selected = true;
                container.DeleteSelected();
            }
        }

        public override void Undo(InkStrokeContainer container)
        {
            foreach (var stroke in _strokes)
            {
                container.AddStroke(stroke.Clone());
                stroke.ActiveStroke.Selected = true; // select it so InkToolbar deletion works properly
            }

            // Unselect everything to stop container.DeleteSelected() deleting everything
            foreach (var stroke in _strokes)
                stroke.ActiveStroke.Selected = false;
        }
    }

    public class CalligraphyHistoryItem
    {
        /// <summary>
        /// Only for use by VS Designer
        /// </summary>
        public CalligraphyHistoryItem()
        {
            if (DesignMode.DesignModeEnabled is false)
                throw new InvalidOperationException("Only for use by VS Designer");
        }

        public CalligraphyHistoryItem(IReadOnlyList<InkStroke> strokes, double fontSize, string text, Rect bounds)
        {
            _strokes = strokes.Select(s => s.Clone()).ToList();
            FontSize = fontSize;
            Text = text;
            Bounds = bounds;
        }

        private IReadOnlyList<InkStroke> _strokes { get; }

        public BitmapImage Thumbnail { get; set; }

        public double FontSize { get; }

        public string Text { get; }

        public Rect Bounds { get; }

        public List<InkStroke> GetStrokes()
        {
            return _strokes.Select(s => s.Clone()).ToList();
        }
    }

}
