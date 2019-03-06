using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.ViewManagement;
using CharacterMap.Core;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI.Text;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using CharacterMapCX;
using GalaSoft.MvvmLight.Ioc;

namespace CharacterMap.ViewModels
{
    public enum ExportStyle
    {
        Black,
        White,
        ColorGlyph
    }

    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        private InstalledFont _selectedFont;

        public IDialogService DialogService { get; }

        public MainViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
            AppNameVersion = GetAppDescription();
            CommandToggleFullScreen = new RelayCommand(ToggleFullScreenMode);
            Load();
        }

        private string GetAppDescription()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{package.DisplayName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision} ({Architecture})";
        }

        public string Architecture => Edi.UWP.Helpers.Utils.Architecture;

        public RelayCommand CommandToggleFullScreen { get; set; }

        private string _appNameVersion;
        public string AppNameVersion
        {
            get => _appNameVersion;
            set => Set(ref _appNameVersion, value);
        }

        private int _fontListFilter;
        public int FontListFilter
        {
            get => _fontListFilter;
            set { if (Set(ref _fontListFilter, value)) RefreshFontList(); }
        }

        public string TitlePrefix
        {
            get => _titlePrefix;
            set => Set(ref _titlePrefix, value);
        }

        private bool _isLoadingFonts;
        public bool IsLoadingFonts
        {
            get => _isLoadingFonts;
            set => Set(ref _isLoadingFonts, value);
        }

        private List<InstalledFont> _fontList;
        public List<InstalledFont> FontList
        {
            get => _fontList;
            set
            {
                if (_fontList != value)
                {
                    _fontList = value;
                    CreateFontListGroup();
                    RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<AlphaKeyGroup<InstalledFont>> GroupedFontList
        {
            get => _groupedFontList;
            set => Set(ref _groupedFontList, value);
        }

        public bool ShowSymbolFontsOnly
        {
            get => App.AppSettings.ShowSymbolFontsOnly;
            set
            {
                App.AppSettings.ShowSymbolFontsOnly = value;
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
                    TitlePrefix = value.Name + " - ";
                    App.AppSettings.LastSelectedFontName = value.Name;
                }

                RaisePropertyChanged();
            }
        }


        private bool _isDarkAccent;
        private string _titlePrefix;

        public bool IsDarkAccent
        {
            get => IsAccentColorDark();
            set { _isDarkAccent = value; RaisePropertyChanged(); }
        }

        private bool IsAccentColorDark()
        {
            var uiSettings = new UISettings();
            var c = uiSettings.GetColorValue(UIColorType.Accent);
            var isDark = (5 * c.G + 2 * c.R + c.B) <= 8 * 128;
            return isDark;
        }



        private async void Load()
        {
            IsLoadingFonts = true;
            await FontFinder.LoadFontsAsync();
            RefreshFontList();
            IsLoadingFonts = false;
        }

        

        private void ToggleFullScreenMode()
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
                view.ExitFullScreenMode();
            else
                view.TryEnterFullScreenMode();
        }

        public void RefreshFontList()
        {
            try
            {
                var fontList = FontFinder.Fonts.AsEnumerable();

                if (FontListFilter == 1)
                    fontList = fontList.Where(f => f.IsSymbolFont);
                else if (FontListFilter == 2)
                    fontList = fontList.Where(f => f.HasImportedFiles);

                FontList = fontList.ToList();
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
                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1));
                GroupedFontList = list.ToObservableCollection();

                if (!FontList.Any()) return;
                if (!string.IsNullOrEmpty(App.AppSettings.LastSelectedFontName))
                {
                    var lastSelectedFont = FontList.FirstOrDefault((i => i.Name == App.AppSettings.LastSelectedFontName));

                    if (null != lastSelectedFont)
                    {
                        this.SelectedFont = lastSelectedFont;

                        //var lastSelectedChar = Chars.FirstOrDefault((i => i.UnicodeIndex == App.AppSettings.LastSelectedCharIndex));
                        //if (null != lastSelectedChar)
                        //{
                        //    this.SelectedChar = lastSelectedChar;
                        //}
                    }
                    else
                    {
                        SelectedFont = FontList.FirstOrDefault();
                    }
                }
                else
                {
                    SelectedFont = FontList.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font Group");
            }
        }

       

        internal async void TryRemoveFont(InstalledFont font)
        {
            IsLoadingFonts = true;

            /* Yes, this is hack. The UI needs to time to remove references to the 
             * current Font otherwise we won't be able to delete it because the file will 
             * be "in use". 16ms works fine on my test machines, but better safe than
             * sorry - this isn't a fast operation in sum anyway because we reload
             * all fonts, so extra 150ms is nothing...
             */
            SelectedFont = FontFinder.DefaultFont;
            await Task.Delay(150);


            bool result = await FontFinder.RemoveFontAsync(font);
            RefreshFontList();

            IsLoadingFonts = false;

            if (!result)
            {
                /* looks like we couldn't delete some fonts :'(. 
                 * We'll get em next time the app launches! */

                _ = DialogService.ShowMessage(
                    "Some fonts could not be completely removed right now. These fonts will not show in the application and will be completely removed next time the app is launched.",
                    "Notice");
            }
        }

        

    }
}