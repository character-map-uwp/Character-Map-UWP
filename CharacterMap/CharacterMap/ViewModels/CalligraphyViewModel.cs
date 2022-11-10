using CharacterMap.Core;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Media.Imaging;

namespace CharacterMap.ViewModels
{
    public class CalligraphyHistoryItem
    {
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


    public class CalligraphyViewModel : ViewModelBase
    {
        public string Text { get => Get<string>(); set => Set(value); }

        public FontVariant Face { get; }

        public ObservableCollection<CalligraphyHistoryItem> Histories { get; }

        public CalligraphyViewModel(CharacterRenderingOptions options)
        {
            Face = options.Variant;
            Histories = new ObservableCollection<CalligraphyHistoryItem>();
        }
    }
}
