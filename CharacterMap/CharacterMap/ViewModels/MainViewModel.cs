using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using CharacterMap.Core;
using System.Collections.Generic;
using CharacterMap.Helpers;
using Windows.Storage;
using CharacterMap.Services;
using CharacterMap.Models;
using CharacterMap.Controls;
using CharacterMapCX;
using CharacterMap.Views;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;

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

        private BasicFontFilter _fontListFilter = BasicFontFilter.All;
        public BasicFontFilter FontListFilter
        {
            get => _fontListFilter;
            set { if (Set(ref _fontListFilter, value)) RefreshFontList(); }
        }

        private UserFontCollection _selectedCollection;
        public UserFontCollection SelectedCollection
        {
            get => _selectedCollection;
            set 
            { 
                if (value != null && value.IsSystemSymbolCollection)
                {
                    FontListFilter = BasicFontFilter.SymbolFonts;
                    return;
                }

                if (Set(ref _selectedCollection, value) && value != null)
                    RefreshFontList(value);
            }
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

        private bool _isLoadingFontsFailed;
        public bool IsLoadingFontsFailed
        {
            get => _isLoadingFontsFailed;
            set => Set(ref _isLoadingFontsFailed, value);
        }

        private bool _hasFonts;
        public bool HasFonts
        {
            get => _hasFonts;
            set => Set(ref _hasFonts, value);
        }

        private bool _isFontSetExpired;
        public bool IsFontSetExpired
        {
            get => _isFontSetExpired;
            set => Set(ref _isFontSetExpired, value);
        }

        private bool _isCollectionExportEnabled = true;
        public bool IsCollectionExportEnabled
        {
            get => _isCollectionExportEnabled;
            set => Set(ref _isCollectionExportEnabled, value);
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
                    OnPropertyChanged();
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

                OnPropertyChanged();
            }
        }

        public UserCollectionsService FontCollections { get; }

        private Exception _startUpException = null;

        #endregion

        public MainViewModel(IDialogService dialogService, AppSettings settings)
        {
            DialogService = dialogService;
            Settings = settings;

            CommandToggleFullScreen = new RelayCommand(ToggleFullScreenMode);

            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            InitialLoad = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsLoadingFonts = true;

            try
            {
                await Task.WhenAll(
                    GlyphService.InitializeAsync(),
                    FontFinder.LoadFontsAsync(),
                    FontCollections.LoadCollectionsAsync());

                var interop = Utils.GetInterop();
                interop.FontSetInvalidated += FontSetInvalidated;

                RefreshFontList();
            }
            catch (Exception ex)
            {
                // For whatever reason, this exception doesn't get caught by the app's
                // UnhandledExceptionHandler, so we need to manually catch and handle it.
                _startUpException = ex;
                ShowStartUpException();
                IsLoadingFonts = false;
                IsLoadingFontsFailed = true;
                return;
            }

            IsLoadingFonts = false;
        }

        private void FontSetInvalidated(NativeInterop sender, object args)
        {
            _ = MainPage.MainDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                IsFontSetExpired = true;
            });
        }

        public void ShowStartUpException()
        {
            UnhandledExceptionDialog.Show(_startUpException);
        }

        public void ReloadFontSet()
        {
            _ = ReloadFontSetAsync();
        }

        public async Task ReloadFontSetAsync()
        {
            IsLoadingFonts = true;
            IsFontSetExpired = false;
            SelectedFont = FontFinder.DefaultFont;
            await FontFinder.LoadFontsAsync();
            RefreshFontList(SelectedCollection);
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
                    FilterTitle = FontListFilter.FilterTitle;

                    if (FontListFilter == BasicFontFilter.ImportedFonts)
                        fontList = FontFinder.ImportedFonts;
                    else
                        fontList = FontListFilter.Query(fontList, FontCollections);
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
                GroupedFontList = new ObservableCollection<AlphaKeyGroup<InstalledFont>>(list);
                HasFonts = FontList.Count > 0;

                if (FontList.Count == 0)
                {
                    SelectedFont = null;
                    return;
                }

                if (!string.IsNullOrEmpty(lastSelected))
                {
                    if (SelectedFont == null || lastSelected != SelectedFont.Name)
                    {
                        var lastSelectedFont = FontList.FirstOrDefault((i => i.Name == lastSelected));
                        SelectedFont = lastSelectedFont ?? FontList.FirstOrDefault();
                    }
                    else
                    {
                        OnPropertyChanged("FontSelectionDebounce");
                    }
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
                Messenger.Send(
                    new AppNotificationMessage(true, Localization.Get("FontsClearedOnNextLaunchNotice"), 6000));
            }
        }

        internal async void ExportAsZip()
        {
            IsCollectionExportEnabled = false;

            try
            {
                await ExportManager.ExportCollectionAsZipAsync(FontList, SelectedCollection);
            }
            finally
            {
                IsCollectionExportEnabled = true;
            }
        }

        internal async void ExportAsFolder()
        {
            IsCollectionExportEnabled = false;

            try
            {
                await ExportManager.ExportCollectionToFolderAsync(FontList);
            }
            finally
            {
                IsCollectionExportEnabled = true;
            }
        }
    }
}