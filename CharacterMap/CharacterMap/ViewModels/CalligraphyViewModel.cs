using CharacterMap.Core;
using CharacterMap.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using System.ComponentModel;

namespace CharacterMap.ViewModels
{
    public partial class CalligraphyViewModel : ViewModelBase
    {
        [ObservableProperty] private bool _isOverlayVisible = true;

        [ObservableProperty] private string _text;

        [ObservableProperty] private double _fontSize = 220d;

        public FontVariant Face { get; }

        [ObservableProperty] InkStrokeManager _inkManager = null;

        public ObservableCollection<CalligraphyHistoryItem> Histories { get; }

        public CalligraphyViewModel(CharacterRenderingOptions options)
        {
            Face = options.Variant;
            Histories = new ObservableCollection<CalligraphyHistoryItem>();
        }

        void EnsureManager(InkStrokeContainer container)
        {
            if (_inkManager is null)
                InkManager = new InkStrokeManager(container);
        }

        public async Task AddToHistoryAsync(InkStrokeContainer container)
        {
            // 1. Create a history item
            CalligraphyHistoryItem h = new(container.GetStrokes(), FontSize, Text);

            // 2. Render a thumbnail of the drawing
            using MemoryStream m = new();
            await container.SaveAsync(m.AsOutputStream());
            m.Seek(0, SeekOrigin.Begin);

            BitmapImage b = new();
            await b.SetSourceAsync(m.AsRandomAccessStream());

            // 3. Add history item
            h.Thumbnail = b;
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

        public async Task SaveAsync(IReadOnlyList<InkStroke> strokes, ExportFormat format, ICanvasResourceCreatorWithDpi device, double width, double height)
        {
            // 1. Setup and render ink strokes
            bool isPng = format == ExportFormat.Png;
            using CanvasSvgDocument svgDocument = new(device);
            using CanvasRenderTarget target = new CanvasRenderTarget(device, (float)width, (float)height);
            using (var ds = target.CreateDrawingSession())
            {
                if (isPng)
                    RenderBitmap(strokes, ds);
                else
                    RenderSVG(strokes, svgDocument, ds);
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

        private static void RenderBitmap(IReadOnlyList<InkStroke> strokes, CanvasDrawingSession ds)
        {
            ds.Clear(Colors.Transparent);
            ds.DrawInk(strokes);
        }

        private static void RenderSVG(IReadOnlyList<InkStroke> strokes, CanvasSvgDocument svgDocument, CanvasDrawingSession ds)
        {
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

        private Stack<InkCommandBase> _redoStack { get; } = new();
        private Stack<InkCommandBase> _undoStack { get; } = new();
        private HashSet<InkStrokeReference> _strokeSet { get; } = new HashSet<InkStrokeReference>();

        private InkStrokeContainer _container;

        public InkStrokeManager(InkStrokeContainer container)
        {
            _container = container;
        }

        public void Clear()
        {
            _strokeSet.Clear();
            _container.Clear();

            _redoStack.Clear();
            _undoStack.Clear();

            UpdateControls();
        }

        public InkStrokeReference GetReference(InkStroke stroke)
        {
            if (FindReference(stroke) is InkStrokeReference r)
                return r;

            r = new InkStrokeReference(stroke);
            _strokeSet.Add(r);
            return r;
        }

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
            _undoStack.Push(new StrokeErasedCommand(this, strokes));
            _redoStack.Clear();
            UpdateControls();
        }

        internal void OnDrawn(IReadOnlyList<InkStroke> strokes)
        {
            _undoStack.Push(new StrokeDrawnCommand(this, strokes));
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
    }

    public abstract class InkCommandBase
    {
        public abstract void Undo(InkStrokeContainer container);
        public abstract void Redo(InkStrokeContainer container);
    }

    public class StrokeDrawnCommand : InkCommandBase
    {
        private readonly List<InkStrokeReference> _strokes;

        public StrokeDrawnCommand(InkStrokeManager manager, IEnumerable<InkStroke> strokes)
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

    public class StrokeErasedCommand : InkCommandBase
    {
        private readonly List<InkStrokeReference> _strokes;

        public StrokeErasedCommand(InkStrokeManager manager, IEnumerable<InkStroke> strokes)
        {
            _strokes = strokes.Select(s => manager.GetReference(s)).ToList();
        }

        public override void Redo(InkStrokeContainer container)
        {
            // This relies on stack being correctly ordered
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
                throw new InvalidOperationException();
        }

        public CalligraphyHistoryItem(IReadOnlyList<InkStroke> strokes, double fontSize, string text)
        {
            _strokes = strokes.Select(s => s.Clone()).ToList();
            FontSize = fontSize;
            Text = text;
        }

        private IReadOnlyList<InkStroke> _strokes { get; }

        public BitmapImage Thumbnail { get; set; }

        public double FontSize { get; }

        public string Text { get; }

        public List<InkStroke> GetStrokes()
        {
            return _strokes.Select(s => s.Clone()).ToList();
        }
    }

}
