using System.Collections.ObjectModel;
using System.Linq;
using CharacterMap.Core;
using Edi.UWP.Helpers;
using GalaSoft.MvvmLight;

namespace CharacterMap.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public AppSettings AppSettings { get; set; }

        private ObservableCollection<InstalledFont> _fontList;
        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        private ObservableCollection<Character> _chars;
        private InstalledFont _selectedFont;

        public ObservableCollection<InstalledFont> FontList
        {
            get
            {
                return _fontList;
            }
            set
            {
                _fontList = value;
                CreateFontListGroup();
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<AlphaKeyGroup<InstalledFont>> GroupedFontList
        {
            get { return _groupedFontList; }
            set { _groupedFontList = value; RaisePropertyChanged(); }
        }

        public ObservableCollection<Character> Chars
        {
            get { return _chars; }
            set { _chars = value; RaisePropertyChanged(); }
        }

        public bool ShowSymbolFontsOnly
        {
            get
            {
                return AppSettings.ShowSymbolFontsOnly;
            }
            set
            {
                AppSettings.ShowSymbolFontsOnly = value;
                RefreshFontList();
                RaisePropertyChanged();
            }
        }

        public InstalledFont SelectedFont
        {
            get
            {
                return _selectedFont;
            }
            set
            {
                _selectedFont = value;

                if (null != _selectedFont)
                {
                    var chars = _selectedFont.GetCharacters();
                    Chars = chars.ToObservableCollection();
                }

                RaisePropertyChanged();
            }
        }

        public MainViewModel()
        {
            AppSettings = new AppSettings();
            RefreshFontList();
        }

        private void RefreshFontList()
        {
            var fontList = InstalledFont.GetFonts();
            FontList = fontList.Where(f => f.IsSymbolFont || !ShowSymbolFontsOnly)
                                  .OrderBy(f => f.Name)
                                  .ToObservableCollection();
        }

        private void CreateFontListGroup()
        {
            var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1), true);
            GroupedFontList = list.ToObservableCollection();
        }
    }
}
