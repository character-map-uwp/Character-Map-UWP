using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.Views;
using CharacterMapCX;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;

namespace CharacterMap.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        public event EventHandler FontListCreated;

        private Debouncer _searchDebouncer { get; } = new Debouncer();

        private Debouncer _settingsDebouncer { get; } = new Debouncer();

        private Exception _startUpException = null;

        #region Properties

        public Task InitialLoad { get; }

        public AppSettings Settings { get; }

        public IDialogService DialogService { get; }

        public RelayCommand CommandToggleFullScreen { get; }

        public UserCollectionsService FontCollections { get; }

        public ObservableCollection<FontItem> Fonts { get; } = new();

        public bool IsSecondaryView { get; }

        [ObservableProperty] int _tabIndex = 0;
        [ObservableProperty] double _progress = 0d;
        [ObservableProperty] string _titlePrefix;
        [ObservableProperty] string _fontSearch;
        [ObservableProperty] string _filterTitle;
        [ObservableProperty] bool _isLoadingFonts;
        [ObservableProperty] bool _isSearchResults;
        [ObservableProperty] bool _isLoadingFontsFailed;
        [ObservableProperty] bool _hasFonts;
        [ObservableProperty] bool _isFontSetExpired;
        [ObservableProperty] bool _isCollectionExportEnabled = true;
        [ObservableProperty] ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        [ObservableProperty] BasicFontFilter _fontListFilter = BasicFontFilter.All;
        [ObservableProperty] List<InstalledFont> _fontList;

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

        private InstalledFont _selectedFont;
        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                _selectedFont = value;
                if (null != _selectedFont)
                    TitlePrefix = value.Name + " -";
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

            Fonts.CollectionChanged += Fonts_CollectionChanged;
        }

        private void Fonts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 1. Ensure child items are listened too
            if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace)
            {
                foreach (var item in e.OldItems.Cast<FontItem>())
                    item.PropertyChanged -= Item_PropertyChanged;
            }
            else if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in e.NewItems.Cast<FontItem>())
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            // 2. Save current open items
            Save();

            ///
            /// HELPERS 
            /// 
            void Item_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                // If the selected variant is changed, resave the font list 
                if (e.PropertyName is nameof(FontItem.Selected) or nameof(FontItem.DisplayMode))
                    Save();
            }

            void Save()
            {
                _settingsDebouncer.Debounce(200, () =>
                {
                    Settings.LastOpenFonts = Fonts.SelectMany(f => 
                        new List<String> { 
                            f.Font.Name, 
                            f.Font.Variants.IndexOf(f.Selected).ToString(), 
                            ((int)f.DisplayMode).ToString() 
                        }).ToList();
                    Settings.LastTabIndex = TabIndex;
                    Settings.LastSelectedFontName = SelectedFont.Name;
                });
            }
        }

        protected override void OnPropertyChangeNotified(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(FontList):
                    CreateFontListGroup();
                    break;
                case nameof(FontListFilter):
                    RefreshFontList();
                    break;
                case nameof(TabIndex) when TabIndex > -1:
                    Settings.LastTabIndex = TabIndex;
                    break;
                case nameof(SelectedFont) when SelectedFont is not null:
                    Settings.LastSelectedFontName = SelectedFont.Name;
                    break;
                case nameof(FontSearch):
                    _searchDebouncer.Debounce(FontSearch.Length == 0 ? 100 : 500, () => RefreshFontList(SelectedCollection));
                    break;
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
                if (isFirstLoad)
                    RestoreOpenFonts();
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

        private void RestoreOpenFonts()
        {
            if (Settings.LastOpenFonts is IList<String> list && list.Count > 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    FontItem item = new(FontFinder.FontDictionary[list[i]]);
                    item.Selected = item.Font.Variants[Convert.ToInt32(list[++i])];
                    item.DisplayMode = (FontDisplayMode)Convert.ToInt32(list[++i]);
                    Fonts.Add(item);
                }

                TabIndex = Settings.LastTabIndex;
                SelectedFont = FontFinder.FontDictionary[Settings.LastSelectedFontName];
            }
        }

        private void CreateFontListGroup()
        {
            try
            {
                // 1. Cache last selected now as setting GroupedFontList can change it.
                InstalledFont selected = SelectedFont;

                // 2. Group the font list
                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1));
                GroupedFontList = new (list);
                HasFonts = FontList.Count > 0;

                // 3. If empty, close everything
                if (FontList.Count == 0)
                {
                    SelectedFont = null;
                    Fonts.Clear();
                    return;
                }

                // 4. Set the correct selected font and remove tabs that are no longer in the list
                if (selected is not null)
                {
                    // 4.1. Clear tabs
                    foreach (var font in Fonts.ToList())
                    {
                        font.Compact = FontList.Contains(font.Font) is false;
                        //if (FontList.Contains(font.Font) is false)
                            //Fonts.Remove(font);
                    }

                    // 4.2. Handle selected font
                    if (SelectedFont == null || selected != SelectedFont)
                    {
                        var lastSelectedFont = FontList.Contains(selected);
                        SelectedFont = FontList.Contains(selected) ? selected : FontList.FirstOrDefault();
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

            // Remove from open tabs
            var items = Fonts.Where(f => f.Font == font).ToList();
            foreach (var item in items)
                Fonts.Remove(item);

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
    }

    /// <summary>
    /// A wrapper used to allow us to change which font is open in a tab
    /// </summary>
    public partial class FontItem : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Tooltip))]
        private string _subTitle;

        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(Tooltip))]
        private InstalledFont _font;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTypeRamp))]
        private FontDisplayMode _displayMode = FontDisplayMode.CharacterMap;

        [ObservableProperty]
        private bool _compact;

        public string Tooltip => $"{Font.Name} {_subTitle}";

        public bool IsTypeRamp => DisplayMode == FontDisplayMode.TypeRamp;

        private FontVariant _selected;
        public FontVariant Selected
        {
            get => _selected;
            set
            {
                if (_selected != value && value is not null)
                {
                    _selected = value;
                    OnPropertyChanged();
                }
            }
        }

        public FontItem(InstalledFont font)
        {
            _font = font;
            _selected = font.DefaultVariant;
        }

        /// <summary>
        /// Only for use by VS designer
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public FontItem()
        {
            if (DesignMode.DesignModeEnabled is false)
                throw new InvalidOperationException("Constructor only for use by designer");
        }

        public void SetFont(InstalledFont font)
        {
            if (font != Font)
            {
                Font = font;
                Selected = font.DefaultVariant;
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(Selected))
            {
                if (Selected != Font.DefaultVariant)
                    SubTitle = Selected.PreferredName;
                else
                    SubTitle = "";
            }
        }
    }

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
}