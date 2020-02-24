using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.ViewManagement;
using CharacterMap.Core;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using System.Collections.Generic;
using CharacterMap.Helpers;
using Windows.Storage;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using CharacterMap.Services;
using CommonServiceLocator;
using System.Text;

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

        private string _collectionTitle;
        public string CollectionTitle
        {
            get => _collectionTitle;
            set { if (Set(ref _collectionTitle, value)) OnCollectionTitleChanged(); }
        }

        private string _filterTitle;
        public string FilterTitle
        {
            get => _filterTitle;
            set => Set(ref _filterTitle, value); 
        }

        private bool _isCollectionTitleValid;
        public bool IsCollectionTitleValid
        {
            get => _isCollectionTitleValid;
            set => Set(ref _isCollectionTitleValid, value);
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

            AppNameVersion = Utils.GetAppDescription();
            CommandToggleFullScreen = new RelayCommand(ToggleFullScreenMode);
            MessengerInstance.Register<ImportMessage>(this, OnFontImportRequest);

            FontCollections = ServiceLocator.Current.GetInstance<UserCollectionsService>();
            InitialLoad = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsLoadingFonts = true;

            await Task.WhenAll(
                GlyphService.InitializeAsync(),
                FontFinder.LoadFontsAsync(),
                ServiceLocator.Current.GetInstance<UserCollectionsService>().LoadCollectionsAsync());

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
                    if (FontListFilter == 1)
                    {
                        fontList = fontList.Where(f => f.IsSymbolFont || FontCollections.SymbolCollection.Fonts.Contains(f.Name));
                        FilterTitle = Localization.Get("OptionSymbolFonts/Text");
                    }
                    else if (FontListFilter == 2)
                    {
                        fontList = fontList.Where(f => f.HasImportedFiles);
                        FilterTitle = Localization.Get("OptionImportedFonts/Text");
                    }
                    else if (FontListFilter == 3)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.FontFace.IsMonospaced);
                        FilterTitle = Localization.Get("OptionMonospacedFonts/Text");
                    }
                    else if (FontListFilter == 4)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.Panose.IsSerifStyle);
                        FilterTitle = Localization.Get("OptionSerifFonts/Text");
                    }
                    else if (FontListFilter == 5)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.Panose.IsSansSerifStyle);
                        FilterTitle = Localization.Get("OptionSansSerifFonts/Text");
                    }
                    else if (FontListFilter == 6)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.DirectWriteProperties.Source == CharacterMapCX.DWriteFontSource.AppxPackage);
                        FilterTitle = "From Microsoft Store";
                    }
                    else if (FontListFilter == 7)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.DirectWriteProperties.Source == CharacterMapCX.DWriteFontSource.RemoteFontProvider);
                        FilterTitle = "Cloud-based Fonts";
                    }
                    else if (FontListFilter == 8)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.Panose.Family == PanoseFamily.Decorative);
                        FilterTitle = "Decorative Fonts";
                    }
                    else if (FontListFilter == 9)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.Panose.Family == PanoseFamily.Script);
                        FilterTitle = "Script Fonts";
                    }
                    else if (FontListFilter == 10)
                    {
                        fontList = fontList.Where(f => f.DefaultVariant.DirectWriteProperties.IsColorFont);
                        FilterTitle = "Color Fonts";
                    }
                    else
                        FilterTitle = Localization.Get("OptionAllFonts/Text");
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

                _ = DialogService.ShowMessage(
                    Localization.Get("FontsClearedOnNextLaunchNotice"),
                    Localization.Get("NoticeLabel/Text"));
            }
        }

        private void OnFontImportRequest(ImportMessage msg)
        {
            _ = CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                IsLoadingFonts = true;
                try
                {
                    if (InitialLoad.IsCompleted)
                    {
                        RefreshFontList();
                    }
                    else
                    {
                        await InitialLoad;
                        await Task.Delay(50);
                    }

                    TrySetSelectionFromImport(msg.Result);
                }
                finally
                {
                    IsLoadingFonts = false;
                }
            });
        }

        private void OnCollectionTitleChanged()
        {
            IsCollectionTitleValid = !string.IsNullOrWhiteSpace(CollectionTitle);
        }
    }
}