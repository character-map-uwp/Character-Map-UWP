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
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Windows.ApplicationModel.Core;
using Windows.Storage.Pickers;
using Windows.System;

namespace CharacterMap.ViewModels
{
    public class MainViewModelArgs
    {
        public MainViewModelArgs(IDialogService dialogService, AppSettings settings, FolderContents folder)
        {
            DialogService = dialogService;
            Settings = settings;
            Folder = folder;
        }

        public IDialogService DialogService { get; }
        public AppSettings Settings { get; }
        public FolderContents Folder { get; }
    }


    public class MainViewModel : ViewModelBase
    {
        public event EventHandler FontListCreated;

        private Debouncer _searchDebouncer { get; } = new Debouncer();

        private Exception _startUpException = null;

        #region Properties

        public Task InitialLoad { get; }

        public AppSettings Settings { get; }

        public IDialogService DialogService { get; }

        public RelayCommand CommandToggleFullScreen { get; }

        public UserCollectionsService FontCollections { get; }


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

        public double Progress      { get => GetV(0d); set => Set(value); }

        public string TitlePrefix   { get => Get<string>(); set => Set(value); }
        public string FontSearch    { get => Get<string>(); set => Set(value); }
        public string FilterTitle   { get => Get<string>(); set => Set(value); }

        public bool IsSecondaryView         { get; }
        public bool IsLoadingFonts          { get => GetV(false); set => Set(value); }
        public bool IsSearchResults         { get => GetV(false); set => Set(value); }
        public bool IsLoadingFontsFailed    { get => GetV(false); set => Set(value); }
        public bool HasFonts                { get => GetV(false); set => Set(value); }
        public bool IsFontSetExpired        { get => GetV(false); set => Set(value); }

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

        #endregion

        public FolderContents Folder { get; set; } = null;

        // This constructor is used by the IoC container;
        public MainViewModel(IDialogService dialogService, AppSettings settings)
            : this(new MainViewModelArgs(dialogService, settings, null)) { }

        public MainViewModel(MainViewModelArgs args)
        {
            DialogService = args.DialogService;
            Settings = args.Settings;

            if (args.Folder is not null)
            {
                IsSecondaryView = true;
                Folder = args.Folder;
            }

            CommandToggleFullScreen = new RelayCommand(Utils.ToggleFullScreenMode);

            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            InitialLoad = LoadAsync(true);
        }

        protected override void OnPropertyChangeNotified(string propertyName)
        {
            if (propertyName == nameof(FontSearch))
            {
                _searchDebouncer.Debounce(FontSearch.Length == 0 ? 100 : 500, () => RefreshFontList(SelectedCollection));
            }
        }

        private async Task LoadAsync(bool isFirstLoad = false)
        {
            IsLoadingFonts = true;

            try
            {
                if (IsSecondaryView is false)
                {
                    _ = Utils.DeleteAsync(ApplicationData.Current.TemporaryFolder);
                    await Task.WhenAll(
                        GlyphService.InitializeAsync(),
                        FontFinder.LoadFontsAsync(!isFirstLoad),
                        FontCollections.LoadCollectionsAsync());

                    NativeInterop interop = Utils.GetInterop();
                    interop.FontSetInvalidated += FontSetInvalidated;
                }

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
            // Bug #152: Sometimes XAML font cache doesn't update after a new font is installed on system.
            // Currently only way to force this is to reload the app from scratch
            //_ = ReloadFontSetAsync();
            _ = CoreApplication.RequestRestartAsync(string.Empty);
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

        public void RefreshFontList(UserFontCollection collection = null)
        {
            try
            {
                IEnumerable<InstalledFont> fontList = Folder?.Fonts ?? FontFinder.Fonts;

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

                if (!string.IsNullOrWhiteSpace(FontSearch))
                {
                    fontList = fontList.Where(f => f.Name.Contains(FontSearch, StringComparison.OrdinalIgnoreCase));
                    string prefix = FontListFilter == BasicFontFilter.All ? "" : FontListFilter.FilterTitle + " ";
                    FilterTitle = $"{(collection != null ? collection.Name + " " : prefix)}\"{FontSearch}\"";
                    IsSearchResults = true;
                }
                else
                    IsSearchResults = false;

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
                // Cache last selected now as setting GroupedFontList can change it.
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

        public void OpenSourceFolder()
        {
            if (Folder is not null)
                _ = Folder.LaunchSourceAsync();
        }

        //public async void OpenFolder()
        //{
        //    var picker = new FolderPicker();

        //    picker.FileTypeFilter.Add("*");
        //    picker.CommitButtonText = Localization.Get("OpenFontPickerConfirm");
        //    var src = await picker.PickSingleFolderAsync();
        //    if (src  is not null)
        //    {
        //        try
        //        {
        //            IsLoadingFonts = true;

        //            if (await FontFinder.LoadToTempFolderAsync(src) is FolderContents folder && folder.Fonts.Count > 0)
        //            {
        //                await MainPage.CreateWindowAsync(new(
        //                    Ioc.Default.GetService<IDialogService>(), 
        //                    Ioc.Default.GetService<AppSettings>(), 
        //                    folder));
        //            }
        //        }
        //        finally
        //        {
        //            IsLoadingFonts = false;
        //        }
        //    }
        //}
    }
}