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
        [ObservableProperty]
        private bool _hasStrokes;

        public string Text { get => Get<string>(); set => Set(value); }

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
    }
}
