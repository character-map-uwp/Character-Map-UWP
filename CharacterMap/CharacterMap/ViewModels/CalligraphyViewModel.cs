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
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;

namespace CharacterMap.ViewModels
{
    public class CalligraphyHistoryItem
    {
        /// <summary>
        /// Only for use by VS Designer
        /// </summary>
        public CalligraphyHistoryItem()
        {
            if (DesignMode.DesignModeEnabled is false)
                throw new  InvalidOperationException();
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


    public partial class CalligraphyViewModel : ViewModelBase
    {
        private Stack<InkStroke> _redoStack { get; } = new();

        [ObservableProperty] private bool _hasStrokes;

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
            // 1. Get the most recent stroke
            IReadOnlyList<InkStroke> strokes = container.GetStrokes();
            if (strokes.Count > 0)
            {
                // 2. Add it to the redo stack
                _redoStack.Push(strokes[^1]);
                strokes[^1].Selected = true;
                
                // 3. Remove it from the ink canvas
                container.DeleteSelected();

                // 4. Notify UI we can now "Redo"
                CanRedo = true;
                return true;
            }

            return false;
        }

        public bool Redo(InkStrokeContainer container)
        {
            if (_redoStack.Count > 0)
            {
                container.AddStroke(_redoStack.Pop().Clone());
                HasStrokes = true;
                CanRedo = _redoStack.Count > 0;
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
            _redoStack.Clear();
            CanRedo = false;
            HasStrokes = false;
        }

        internal void OnStrokesErased(IReadOnlyList<InkStroke> strokes)
        {
            // Add deleted strokes to the Redo stack
            foreach (var s in strokes)
                _redoStack.Push(s);

            CanRedo = strokes.Count > 0;
        }

        internal void OnStrokeDrawn()
        {
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
}
