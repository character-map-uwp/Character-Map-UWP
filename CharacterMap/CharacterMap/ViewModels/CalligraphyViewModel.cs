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

namespace CharacterMap.ViewModels
{
    public partial class CalligraphyViewModel : ViewModelBase
    {
        private Stack<InkCommandBase> _redoStack { get; } = new();
        private Stack<InkCommandBase> _undoStack { get; } = new();

        [ObservableProperty] private bool _hasStrokes;

        [ObservableProperty] private bool _canUndo;

        [ObservableProperty] private bool _canRedo;

        [ObservableProperty] private bool _isOverlayVisible = true;

        [ObservableProperty] private string _text;

        [ObservableProperty] private double _fontSize = 220d;

        public FontVariant Face { get; }

        public ObservableCollection<CalligraphyHistoryItem> Histories { get; }

        public CalligraphyViewModel(CharacterRenderingOptions options)
        {
            Face = options.Variant;
            Histories = new ObservableCollection<CalligraphyHistoryItem>();
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

        public bool Undo(InkStrokeContainer container)
        {
            if (_undoStack.Count > 0)
            {
                // 1. Get and execute the most recent undo command
                var command = _undoStack.Pop();
                command.Undo(container);
                
                // 2. Push it to the redo stack
                _redoStack.Push(command);

                // 3. Update UI commands
                UpdateControls(container);
                return true;
            }

            return false;
        }

        void UpdateControls(InkStrokeContainer container)
        {
            HasStrokes = container.GetStrokes().Any();
            CanUndo = _undoStack.Count > 0;
            CanRedo = _redoStack.Count > 0;
        }

        public bool Redo(InkStrokeContainer container)
        {
            if (_redoStack.Count > 0)
            {
                // 1. Get and execute the most recent redo command
                var command = _redoStack.Pop();
                command.Redo(container);

                // 2. Push it to the undo stack
                _undoStack.Push(command);

                // 3. Update UI commands
                UpdateControls(container);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clear contents of Ink container and the Redo stack
        /// </summary>
        /// <param name="container"></param>
        public void Clear(InkStrokeContainer container)
        {
            container.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            UpdateControls(container);
        }

        internal void OnStrokesErased(InkStrokeContainer container, IReadOnlyList<InkStroke> strokes)
        {
            _undoStack.Push(new StrokeErasedCommand(strokes));
            _redoStack.Clear();
            UpdateControls(container);
        }

        internal void OnStrokeDrawn(IReadOnlyList<InkStroke> strokes)
        {
            _undoStack.Push(new StrokeDrawnCommand(strokes));
            CanUndo = true;
            HasStrokes = true;

            // When user draws a stroke manually, clear the redo stack
            _redoStack.Clear();
            CanRedo = false;
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

    public abstract class InkCommandBase
    {
        public abstract void Undo(InkStrokeContainer container);
        public abstract void Redo(InkStrokeContainer container);
    }

    public class StrokeDrawnCommand : InkCommandBase
    {
        private readonly List<InkStroke> _strokes;

        public StrokeDrawnCommand(IReadOnlyList<InkStroke> strokes)
        {
            _strokes = strokes.ToList();
        }

        public override void Redo(InkStrokeContainer container)
        {
            for (int i = 0; i < _strokes.Count; i++) 
            {
                var stroke = _strokes[i].Clone();
                container.AddStroke(stroke);
                _strokes[i] = stroke;
            }
        }

        public override void Undo(InkStrokeContainer container)
        {
            // Delete the strokes from the canvas
            var cStrokes = container.GetStrokes().ToList();
            foreach (var stroke in _strokes)
            {
                var cStroke = cStrokes.First(s => s.BoundingRect == stroke.BoundingRect);
                cStroke.Selected = true;
                container.DeleteSelected();
                cStroke.Selected = false;
            }
        }
    }

    public class StrokeErasedCommand : InkCommandBase
    {
        private readonly List<InkStroke> _strokes;

        public StrokeErasedCommand(IReadOnlyList<InkStroke> strokes)
        {
            _strokes = strokes.ToList();
        }

        public override void Redo(InkStrokeContainer container)
        {
            // This relies on stack being correctly ordered
            List<InkStroke> strokes = container.GetStrokes().Reverse().Take(_strokes.Count).ToList();
            foreach (var stroke in strokes)
            {
                stroke.Selected = true;
                container.DeleteSelected();
            }
        }

        public override void Undo(InkStrokeContainer container)
        {
            for (int i = 0; i < _strokes.Count; i++)
            {
                var stroke = _strokes[i].Clone();
                container.AddStroke(stroke);
                stroke.Selected = true; // select it so InkToolbar deletion works properly
                _strokes[i] = stroke;
            }

            // Unselect everything to stop container.DeleteSelected() deleting everything
            foreach (var stroke in _strokes)
                stroke.Selected = false;
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
