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
        private Character _selectedChar;
        private string _xamlCode;
        private string _fontIcon;

        public ObservableCollection<InstalledFont> FontList
        {
            get => _fontList;
            set
            {
                _fontList = value;
                CreateFontListGroup();
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<AlphaKeyGroup<InstalledFont>> GroupedFontList
        {
            get => _groupedFontList;
            set { _groupedFontList = value; RaisePropertyChanged(); }
        }

        public ObservableCollection<Character> Chars
        {
            get => _chars;
            set { _chars = value; RaisePropertyChanged(); }
        }

        public Character SelectedChar
        {
            get => _selectedChar;
            set
            {
                _selectedChar = value;
                if (null != value)
                {
                    XamlCode = $"&#x{value.UnicodeIndex.ToString("x").ToUpper()};";
                    FontIcon = $@"<FontIcon FontFamily=""{SelectedFont.Name}"" Glyph=""&#x{value.UnicodeIndex.ToString("x").ToUpper()};"" />";
                }
                RaisePropertyChanged();
            }
        }

        public string XamlCode
        {
            get => _xamlCode;
            set { _xamlCode = value; RaisePropertyChanged(); }
        }

        public string FontIcon
        {
            get => _fontIcon;
            set { _fontIcon = value; RaisePropertyChanged(); }
        }

        public bool ShowSymbolFontsOnly
        {
            get => AppSettings.ShowSymbolFontsOnly;
            set
            {
                AppSettings.ShowSymbolFontsOnly = value;
                RefreshFontList();
                RaisePropertyChanged();
            }
        }

        public InstalledFont SelectedFont
        {
            get => _selectedFont;
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
