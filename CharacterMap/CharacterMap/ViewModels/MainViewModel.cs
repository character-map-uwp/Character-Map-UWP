using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CharacterMap.Core;
using CharacterMap.Services;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;

namespace CharacterMap.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<Character> _chars;
        private string _fontIcon;
        private ObservableCollection<InstalledFont> _fontList;
        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        private bool _isLightThemeEnabled;
        private Character _selectedChar;
        private InstalledFont _selectedFont;
        private string _xamlCode;

        public MainViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
            AppSettings = new AppSettings();
            RefreshFontList();
            IsLightThemeEnabled = ThemeSelectorService.IsLightThemeEnabled;
            SwitchThemeCommand = new RelayCommand(async () => { await ThemeSelectorService.SwitchThemeAsync(); });
        }

        public AppSettings AppSettings { get; set; }
        public ICommand SwitchThemeCommand { get; }
        public IDialogService DialogService { get; set; }

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
            set
            {
                _groupedFontList = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<Character> Chars
        {
            get => _chars;
            set
            {
                _chars = value;
                RaisePropertyChanged();
            }
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
                    FontIcon = $@"<FontIcon FontFamily=""{SelectedFont.Name}"" Glyph=""&#x{
                            value.UnicodeIndex.ToString("x").ToUpper()
                        };"" />";
                }
                RaisePropertyChanged();
            }
        }

        public string XamlCode
        {
            get => _xamlCode;
            set
            {
                _xamlCode = value;
                RaisePropertyChanged();
            }
        }

        public string FontIcon
        {
            get => _fontIcon;
            set
            {
                _fontIcon = value;
                RaisePropertyChanged();
            }
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

        public bool IsLightThemeEnabled
        {
            get => _isLightThemeEnabled;
            set => Set(ref _isLightThemeEnabled, value);
        }

        private void RefreshFontList()
        {
            try
            {
                var fontList = InstalledFont.GetFonts();
                FontList = fontList.Where(f => f.IsSymbolFont || !ShowSymbolFontsOnly)
                    .OrderBy(f => f.Name)
                    .ToObservableCollection();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font List");
            }
        }

        private void CreateFontListGroup()
        {
            try
            {
                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1), true);
                GroupedFontList = list.ToObservableCollection();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font Group");
            }
        }
    }
}