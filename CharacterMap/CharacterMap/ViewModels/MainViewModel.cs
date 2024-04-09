using CharacterMap.Controls;
using CharacterMap.Views;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.ConstrainedExecution;
using Windows.ApplicationModel.Core;

namespace CharacterMap.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public event EventHandler FontListCreated;

    private Debouncer _searchDebouncer { get; } = new ();

    private Debouncer _settingsDebouncer { get; } = new ();

    private Exception _startUpException = null;

    #region Properties

    protected override bool TrackAnimation => true;

    public Task InitialLoad { get; }

    public AppSettings Settings { get; }

    public IDialogService DialogService { get; }

    public RelayCommand CommandToggleFullScreen { get; }

    public UserCollectionsService FontCollections { get; }

    public ObservableCollection<FontItem> Fonts { get; } = new();

    public bool IsSecondaryView { get; }

    [NotifyPropertyChangedFor(nameof(CurrentFont))]
    [ObservableProperty] int _tabIndex = 0;
    [ObservableProperty] double _progress = 0d;
    [ObservableProperty] string _titlePrefix;
    [ObservableProperty] string _fontSearch;
    [ObservableProperty] string _filterTitle;
    [ObservableProperty] string _collectionExportProgress;
    [ObservableProperty] bool _canFilter = true;
    [ObservableProperty] bool _isLoadingFonts;
    [ObservableProperty] bool _isSearchResults;
    [ObservableProperty] bool _isLoadingFontsFailed;
    [ObservableProperty] bool _hasFonts;
    [ObservableProperty] bool _isFontSetExpired;
    [ObservableProperty] bool _isCollectionExportEnabled = true;
    [ObservableProperty] ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
    [ObservableProperty] BasicFontFilter _fontListFilter = BasicFontFilter.All;
    [ObservableProperty] List<InstalledFont> _fontList;

    public FontItem CurrentFont => Fonts.Count > 0 && TabIndex < Fonts.Count && TabIndex > -1
        ? Fonts[TabIndex] : null;

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

            if (Set(ref _selectedCollection, value))
            {
                if (value is not null)
                {
                    Settings.LastSelectedCollection = $"{AppSettings.UserCollectionIdentifier}{value.Id}";
                    RefreshFontList(value);
                }
                else
                    Settings.LastSelectedCollection = null;
            }
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

            TitleBarHelper.SetTitle(value?.Name);
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
    protected override void OnPropertyChangeNotified(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(FontList):
                CreateFontListGroup();
                break;
            case nameof(FontListFilter):
                Settings.LastSelectedCollection = FontListFilter?.DisplayTitle;
                RefreshFontList();
                break;
            case nameof(TabIndex) when TabIndex > -1 && IsSecondaryView is false:
                Settings.LastTabIndex = TabIndex;
                break;
            case nameof(SelectedFont) when SelectedFont is not null && IsSecondaryView is false:
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
                interop.FontSetInvalidated -= FontSetInvalidated;
                interop.FontSetInvalidated += FontSetInvalidated;
            }

            if (!IsSecondaryView 
                && isFirstLoad 
                && Settings.RestoreLastCollectionOnLaunch)
            {
                switch(GetLastUsedCollection())
                {
                    case UserFontCollection uc:
                        SelectedCollection = uc;
                        break;
                    case BasicFontFilter filter:
                        FontListFilter = filter;
                        break;
                    default:
                        RefreshFontList();
                        break;
                }
            }
            else
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

    /// <summary>
    /// Can return <see cref="UserFontCollection"/>, <see cref="BasicFontFilter"/> or <see cref="null"/>
    /// </summary>
    /// <returns></returns>
    object GetLastUsedCollection()
    {
        // 1. No collection
        if (Settings.LastSelectedCollection is null)
            return null;

        // 2. User collection
        if (Settings.LastSelectedCollection.StartsWith(AppSettings.UserCollectionIdentifier))
        {
            if (long.TryParse(Settings.LastSelectedCollection.Remove(0, AppSettings.UserCollectionIdentifier.Length), System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out long id))
                return FontCollections.Items.FirstOrDefault(c => c.Id == id);
        }

        // 3. App defined font filter
        //    NOTE: breaks if language changes
        return FilterFlyout.AllFilters.FirstOrDefault(f => f.DisplayTitle == Settings.LastSelectedCollection);
    }

    public async void RefreshFontList(UserFontCollection collection = null)
    {
        if (CanFilter == false)
            return;
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
                {
                    if (FontListFilter.RequiresAsync)
                    {
                        CanFilter = false;
                        fontList = await Task.Run(() => FontListFilter.Query(fontList, FontCollections));
                        FontListFilter.RequiresAsync = false;
                    }
                    else
                        fontList = FontListFilter.Query(fontList, FontCollections);
                }
            }

            if (!string.IsNullOrWhiteSpace(FontSearch))
            {
                if (FontSearch.StartsWith("char:", StringComparison.OrdinalIgnoreCase)
                    && FontSearch.Remove(0, 5).Trim() is string q
                    && !string.IsNullOrWhiteSpace(q))
                {
                    foreach (var ch in q)
                    {
                        if (ch == ' ')
                            continue;
                        fontList = BasicFontFilter.ForChar(new(ch)).Query(fontList, FontCollections);
                    }
                    FilterTitle = $"{FontListFilter.FilterTitle} \"{q}\"";
                }
                else
                {
                    fontList = fontList.Where(f => f.Name.Contains(FontSearch, StringComparison.OrdinalIgnoreCase));
                    string prefix = FontListFilter == BasicFontFilter.All ? "" : FontListFilter.FilterTitle + " ";
                    FilterTitle = $"{(collection != null ? collection.Name + " " : prefix)}\"{FontSearch}\"";
                }
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
        finally
        {
            CanFilter = true;
        }
    }

    private void Fonts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Our primary concern here is saving the Tab list
        // of opened fonts. We only need to do this for the
        // primary window.
        if (IsSecondaryView is false)
        {
            OnPropertyChanged(nameof(CurrentFont));

            // 1. Ensure child items are listened too
            if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace)
            {
                foreach (var item in e.OldItems.Cast<FontItem>())
                    item.PropertyChanged -= Item_PropertyChanged;
            }
            else if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset
                && e.NewItems is not null)
            {
                foreach (var item in e.NewItems.Cast<FontItem>())
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.PropertyChanged += Item_PropertyChanged;

                    if (FontList is not null)
                        item.IsCompact = FontList.Contains(item.Font) is false;
                }
            }

            // 2. Save current open items
            Save();
        }


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
            if (IsSecondaryView)
                return;

            _settingsDebouncer.Debounce(200, () =>
            {
                Settings.LastOpenFonts = Fonts.SelectMany(f =>
                    new List<String> {
                        f.Font.Name,
                        f.Font.Variants.IndexOf(f.Selected).ToString(),
                        ((int)f.DisplayMode).ToString()
                    }).ToList();
                Settings.LastTabIndex = TabIndex;

                if (SelectedFont is not null)
                    Settings.LastSelectedFontName = SelectedFont.Name;
                else if (TabIndex < Fonts.Count && TabIndex >= 0)
                    Settings.LastSelectedFontName = Fonts[TabIndex].Font.Name;

            });
        }
    }

    public void RestoreOpenFonts()
    {
        if (IsSecondaryView is false
            && Settings.LastOpenFonts is IList<String> list
            && list.Count > 0)
        {
            bool removed = false;
            Fonts.Clear();

            // 1. Parse list of saved fonts
            for (int i = 0; i < list.Count; i++)
            {
                // 1.1. Ensure the saved font hasn't been deleted. If it hasn't, 
                //      add it to the list.
                if (FontFinder.FontDictionary.TryGetValue(list[i], out InstalledFont font))
                {
                    // 1.2. The selected font face (or another in the family) may have been
                    //      deleted making the stored index invalid. Make sure the index is
                    //      still within a valid range.
                    int faceIdx = Convert.ToInt32(list[++i]);
                    if ((faceIdx < font.Variants.Count) is false)
                        faceIdx = font.Variants.IndexOf(font.DefaultVariant);

                    Fonts.Add(new(font)
                    {
                        Selected = font.Variants[faceIdx],
                        DisplayMode = (FontDisplayMode)Convert.ToInt32(list[++i])
                    });
                }
                else
                {
                    // Font has probably been uninstalled
                    ++i; // Skip over saved variant
                    ++i; // Skip over saved display mode

                    removed = true;
                }
            }

            // 2. Handle restoring fonts
            if (Fonts.Count == 0)
            {
                // If no fonts have been restored, either this is a first run, or user has uninstalled
                // all the previously open fonts. In this case we use the first font we can find.
                if (FontList.FirstOrDefault() is InstalledFont first)
                {
                    Fonts.Add(new(first));
                    SelectedFont = first;
                    TabIndex = 0;
                }
                else
                {
                    // No fonts, do nuffin'
                }
            }
            else
            {
                int tabIndex = -1;
                // 3. Try to restore SelectedFont & TabIndex.
                //    First, check if the SelectedFont still actually exists, as the user may have
                //    uninstalled it between application runs.
                if (FontFinder.FontDictionary.TryGetValue(Settings.LastSelectedFontName, out InstalledFont last))
                {
                    // 3.1. Restore TabIndex.
                    //      If a font was removed between runs TabIndex may no longer be valid,
                    //      so find the first matching font
                    if (removed)
                        tabIndex = Fonts.Select(f => f.Font).ToList().IndexOf(last);
                    else
                        tabIndex = Settings.LastTabIndex;

                    // 3.2. If TabIndex doesn't match the font, ignore both values and use the first font
                    if (tabIndex == -1 || tabIndex >= Fonts.Count || Fonts[tabIndex].Font != last)
                        tabIndex = 0;
                }
                else
                {
                    // 3.3. The last selected font has been deleted. Use the first one we have.
                    tabIndex = 0;
                }

                // 3.4. Set deduced TabIndex safely
                TabIndex = Math.Max(0, Math.Min(Fonts.Count - 1, tabIndex));

                // 4. Restore SelectedFont. This may not longer match LastSelectedFontName if 
                //    we found out-of-sync values above.
                SelectedFont = Fonts[TabIndex].Font;
            }
        }
        else if (FontList.FirstOrDefault() is InstalledFont first)
        {
            // Fallback to first font available
            Fonts.Add(new(first));
            SelectedFont = first;
            TabIndex = 0;
        }
    }
    public bool IsCreating { get; private set; }
    private void CreateFontListGroup()
    {
        try
        {
            IsCreating = true;

            // 1. Cache last selected now as setting GroupedFontList can change it.
            //    Use TabIndex as SelectedFont may be inaccurate when inside a filter
            //    with tabs that aren't inside the current FontList
            InstalledFont selected =
                Fonts.Count > 0
                        ? (TabIndex > -1 ? Fonts[TabIndex].Font : SelectedFont)
                        : Fonts.FirstOrDefault()?.Font;

            // 2. Group the font list
            var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1));
            GroupedFontList = new(list);
            HasFonts = FontList.Count > 0;

            // 3. If empty, close everything
            if (FontList.Count == 0)
            {
                SelectedFont = null;
                foreach (var font in Fonts.ToList())
                    font.IsCompact = true;
                return;
            }

            // Clear Font List selection on left pane if needed
            if (IsLoadingFonts is false && FontList.Contains(selected) is false)
                SelectedFont = null;

            // 4.1. Update tab size
            foreach (var font in Fonts.ToList())
            {
                font.IsCompact = FontList.Contains(font.Font) is false;
            }

            // 4. Set the correct selected font and remove tabs that are no longer in the list
            if (selected is not null)
            {
                // 4.2. Handle selected font
                if (SelectedFont == null || selected != SelectedFont)
                {
                    var lastSelectedFont = FontList.Contains(selected);
                    SelectedFont = selected;
                }
                else
                {
                    OnPropertyChanged("FontSelectionDebounce");
                }
            }
            else
            {
                //SelectedFont = FontList.FirstOrDefault();
            }

            FontListCreated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            DialogService.ShowMessageBox(
                e.Message, Localization.Get("LoadingFontListError"));
        }

        IsCreating = false;
    }

    public void OpenTab(InstalledFont font)
    {
        Fonts.Insert(TabIndex + 1, new(font));
    }

    public bool TryCloseTab(int idx)
    {
        if (Fonts.Count > 1)
        {
            Fonts.RemoveAt(idx);
            return true;
        }

        return false;
    }

    public void NotifyTabs()
    {
        // Fires a faux notification for changing the "Font" in a FontItem,
        // causing the binding used for choosing which font to display to update.
        foreach (var font in Fonts)
            font.NotifyFontChange();
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
        RestoreOpenFonts();

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
            await ExportManager.ExportCollectionAsZipAsync(
                FontList,
                SelectedCollection,
                p => OnSyncContext(() => CollectionExportProgress = p));
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
            await ExportManager.ExportCollectionToFolderAsync(
                FontList,
                p => OnSyncContext(() => CollectionExportProgress = p));
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