using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using CharacterMap.Core;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using System.Collections.Generic;
using CharacterMap.Helpers;
using Windows.Storage;
using CharacterMap.Services;
using CharacterMap.Models;
using GalaSoft.MvvmLight.Ioc;

namespace CharacterMap.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region Properties

        public event EventHandler FontListCreated;

        public Task InitialLoad { get; }

        public AppSettings Settings { get; }

        public IDialogService DialogService { get; }

        public RelayCommand CommandToggleFullScreen { get; }

        public bool IsDarkAccent => Utils.IsAccentColorDark();

        private int _fontListFilter;
        public int FontListFilter
        {
            get => _fontListFilter;
            set { if (Set(ref _fontListFilter, value)) RefreshFontList(); }
        }

        private UserFontCollection _selectedCollection;
        public UserFontCollection SelectedCollection
        {
            get => _selectedCollection;
            set { if (Set(ref _selectedCollection, value)) if (value != null) RefreshFontList(value); }
        }

        private string _titlePrefix;
        public string TitlePrefix
        {
            get => _titlePrefix;
            set => Set(ref _titlePrefix, value);
        }

        private string _filterTitle;
        public string FilterTitle
        {
            get => _filterTitle;
            set => Set(ref _filterTitle, value); 
        }

        private bool _isLoadingFonts;
        public bool IsLoadingFonts
        {
            get => _isLoadingFonts;
            set => Set(ref _isLoadingFonts, value);
        }

        private bool _hasFonts;
        public bool HasFonts
        {
            get => _hasFonts;
            set => Set(ref _hasFonts, value);
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

        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        public ObservableCollection<AlphaKeyGroup<InstalledFont>> GroupedFontList
        {
            get => _groupedFontList;
            set => Set(ref _groupedFontList, value);
        }

        private InstalledFont _selectedFont;
        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                _selectedFont = value;
                if (null != _selectedFont)
                {
                    TitlePrefix = value.Name + " -";
                    Settings.LastSelectedFontName = value.Name;
                }
                else
                    TitlePrefix = string.Empty;

                RaisePropertyChanged();
            }
        }

        public UserCollectionsService FontCollections { get; }

        #endregion

        public MainViewModel(IDialogService dialogService, AppSettings settings)
        {
            DialogService = dialogService;
            Settings = settings;

            CommandToggleFullScreen = new RelayCommand(ToggleFullScreenMode);

            FontCollections = SimpleIoc.Default.GetInstance<UserCollectionsService>();
            InitialLoad = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsLoadingFonts = true;

            await Task.WhenAll(
                GlyphService.InitializeAsync(),
                FontFinder.LoadFontsAsync(),
                FontCollections.LoadCollectionsAsync());

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

        public void RefreshFontList(UserFontCollection collection = null)
        {
            try
            {
                var fontList = FontFinder.Fonts.AsEnumerable();

                if (collection != null)
                {
                    FilterTitle = collection.Name;
                    fontList = fontList.Where(f => collection.Fonts.Contains(f.Name));
                }
                else
                {
                    SelectedCollection = null;
                    switch (FontListFilter)
                    {
                        case 1:
                            fontList = fontList.Where(f => f.IsSymbolFont || FontCollections.SymbolCollection.Fonts.Contains(f.Name));
                            FilterTitle = Localization.Get("OptionSymbolFonts/Text");
                            break;
                        case 2:
                            fontList = fontList.Where(f => f.HasImportedFiles);
                            FilterTitle = Localization.Get("OptionImportedFonts/Text");
                            break;
                        case 3:
                            fontList = fontList.Where(f => f.DefaultVariant.FontFace.IsMonospaced);
                            FilterTitle = Localization.Get("OptionMonospacedFonts/Text");
                            break;
                        case 4:
                            fontList = fontList.Where(f => f.DefaultVariant.Panose.IsSerifStyle);
                            FilterTitle = Localization.Get("OptionSerifFonts/Text");
                            break;
                        case 5:
                            fontList = fontList.Where(f => f.DefaultVariant.Panose.IsSansSerifStyle);
                            FilterTitle = Localization.Get("OptionSansSerifFonts/Text");
                            break;
                        case 6:
                            fontList = fontList.Where(f => f.DefaultVariant.DirectWriteProperties.Source == CharacterMapCX.DWriteFontSource.AppxPackage);
                            FilterTitle = Localization.Get("OptionAppxFonts/Text");
                            break;
                        case 7:
                            fontList = fontList.Where(f => f.DefaultVariant.DirectWriteProperties.Source == CharacterMapCX.DWriteFontSource.RemoteFontProvider);
                            FilterTitle = Localization.Get("OptionCloudFonts/Text");
                            break;
                        case 8:
                            fontList = fontList.Where(f => f.DefaultVariant.Panose.Family == PanoseFamily.Decorative);
                            FilterTitle = Localization.Get("OptionDecorativeFonts/Text");
                            break;
                        case 9:
                            fontList = fontList.Where(f => f.DefaultVariant.Panose.Family == PanoseFamily.Script);
                            FilterTitle = Localization.Get("OptionScriptFonts/Text");
                            break;
                        case 10:
                            fontList = fontList.Where(f => f.DefaultVariant.DirectWriteProperties.IsColorFont);
                            FilterTitle = Localization.Get("OptionColorFonts/Text");
                            break;
                        default:
                            FilterTitle = Localization.Get("OptionAllFonts/Text");
                            break;
                    }
                }

                FontList = fontList.ToList();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(
                    e.Message, Localization.Get("LoadingFontListError"));
            }
        }

        private void CreateFontListGroup()
        {
            try
            {
                // cache last selected now as setting GroupedFontList can change it.
                string lastSelected = Settings.LastSelectedFontName;

                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1));
                GroupedFontList = list.ToObservableCollection();
                HasFonts = FontList.Count > 0;

                if (FontList.Count == 0)
                {
                    SelectedFont = null;
                    return;
                }

                if (!string.IsNullOrEmpty(lastSelected))
                {
                    var lastSelectedFont = FontList.FirstOrDefault((i => i.Name == lastSelected));
                    SelectedFont = lastSelectedFont ?? FontList.FirstOrDefault();
                }
                else
                {
                    SelectedFont = FontList.FirstOrDefault();
                }

                FontListCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(
                    e.Message, Localization.Get("LoadingFontListError"));
            }
        }

        internal void TrySetSelectionFromImport(FontImportResult result)
        {
            StorageFile file = result.Imported.FirstOrDefault() ?? result.Existing.FirstOrDefault();
            if (file != null
                && FontList.FirstOrDefault(f =>
                f.HasImportedFiles && f.DefaultVariant.FileName == file.Name) is InstalledFont font)
            {
                SelectedFont = font;
                FontListCreated?.Invoke(this, EventArgs.Empty);
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
            if (result)
            {
                _ = FontCollections.RemoveFromAllCollectionsAsync(font);
            }

            RefreshFontList(SelectedCollection);

            IsLoadingFonts = false;

            if (!result)
            {
                /* looks like we couldn't delete some fonts :'(. 
                 * We'll get em next time the app launches! */
                MessengerInstance.Send(
                    new AppNotificationMessage(true, Localization.Get("FontsClearedOnNextLaunchNotice"), 6000));
            }
        }
    }
}