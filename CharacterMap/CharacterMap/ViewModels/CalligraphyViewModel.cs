using CharacterMap.Core;
using CharacterMap.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

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

        public CalligraphyHistoryItem(IReadOnlyList<InkStroke> strokes)
        {
            _strokes = strokes.Select(s => s.Clone()).ToList();
        }

        private IReadOnlyList<InkStroke> _strokes { get; }

        public BitmapImage Thumbnail { get;set;}

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
            CalligraphyHistoryItem h = new(container.GetStrokes());

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
    }
}
