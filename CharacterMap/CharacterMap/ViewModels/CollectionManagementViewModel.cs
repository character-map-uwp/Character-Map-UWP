namespace CharacterMap.ViewModels;

public partial class CollectionManagementViewModel : ViewModelBase
{
    protected override bool CaptureContext => true;

    #region Properties
    List <CMFontFamily> _systemFontList;

    [ObservableProperty] string _collectionExportProgress;
    [ObservableProperty] bool _isSaving = false;
    [ObservableProperty] bool _isExporting = false;


    [ObservableProperty] string _query = null;
    [ObservableProperty] List<IFontCollection> _collections;
    [ObservableProperty] ObservableCollection<CMFontFamily> _fontList;
    [ObservableProperty] ObservableCollection<CMFontFamily> _collectionFonts;

    public bool IsSmartCollection => _selectedCollection is SmartFontCollection;
    public bool IsUserCollection => _selectedCollection is UserFontCollection;

    public bool IsSelectedEditable => SelectedCollection is not null && SelectedCollection != CollectionService.SymbolCollection;

    public ObservableCollection<CMFontFamily> SelectedFonts = new();

    public ObservableCollection<CMFontFamily> SelectedCollectionFonts = new();
    public UserCollectionsService CollectionService { get; private set; } = null;

    private IFontCollection _selectedCollection;
    public IFontCollection SelectedCollection
    {
        get => _selectedCollection;
        set
        {
            if (Set(ref _selectedCollection, value) && value != null)
            {
                OnPropertyChanged(nameof(IsSelectedEditable));
                OnPropertyChanged(nameof(IsSmartCollection));
                OnPropertyChanged(nameof(IsUserCollection));

                if (IsSmartCollection is false)
                    RefreshFontLists();
            }
        }
    }

    #endregion

    private bool _pendingSave = false;
    private Debouncer _debouncer = new();

    partial void OnQueryChanged(string value)
    {
        _debouncer.Debounce(250, RefreshFontList);
    }

    public void Activate()
    {
        if (CollectionService is null)
            CollectionService = Ioc.Default.GetService<UserCollectionsService>();

        RefreshCollections();
        RefreshFontLists();
    }

    public void Deactivate()
    {
        _ = SaveAsync();
        SelectedCollection = null;
        RefreshFontLists();
    }

    public void RefreshCollections()
    {
        // To work around a bug that this is technically
        // the same collection every time we need to 
        // make sure we call .ToList() or UI bindings will
        // not work correctly.
        var collections = CollectionService.All.ToList();
        collections.Add(CollectionService.SymbolCollection);
        Collections = collections.OrderBy(c => c.Name).ToList();
        OnPropertyChanged(nameof(Collections));
    }

    public void RefreshFontLists()
    {
        if (SelectedCollection is not UserFontCollection collection)
        {
            // clear all the things
            CollectionFonts = new();
            FontList = new();
            return;
        }

        Query = null;

        // 1. Get list of fonts in and not in the collection
        var collectionFonts = collection.GetFontFamilies();
        var systemFonts = FontFinder.Fonts.Except(collectionFonts).ToList();

        // 2. Create binding lists
        _systemFontList = systemFonts;
        CollectionFonts = new(collectionFonts);
        RefreshFontList();
    }

    void RefreshFontList()
    {
        // 1. Filter fonts
        var systemFonts = string.IsNullOrWhiteSpace(Query) 
            ?_systemFontList.AsEnumerable()
            : FontFinder.QueryFontList(Query, _systemFontList, CollectionService).FontList;
        
        // 2. Create binding lists
        FontList = new(systemFonts);
    }

    public void AddToCollection()
    {
        if (SelectedFonts is null || SelectedFonts.Count == 0)
            return;

        List<CMFontFamily> fonts = SelectedFonts.ToList();
        foreach (CMFontFamily font in fonts)
        {
            if (FontList.Remove(font))
            {
                _systemFontList?.Remove(font);
                CollectionFonts.AddSorted(font);
            }
        }

        StartSave();
    }

    public void RemoveFromCollection()
    {
        if (SelectedCollectionFonts is null || SelectedCollectionFonts.Count == 0)
            return;

        List<CMFontFamily> fonts = SelectedCollectionFonts.ToList();
        foreach (CMFontFamily font in fonts)
        {
            if (CollectionFonts.Remove(font))
            {
                _systemFontList?.AddSorted(font);
                if (string.IsNullOrWhiteSpace(Query) || font.Name.Contains(Query, StringComparison.InvariantCultureIgnoreCase))
                    FontList.AddSorted(font);
            }
        }

        StartSave();
    }

    public void StartSave()
    {
        _ = SaveAsync();
    }

    async Task SaveAsync()
    {
        if (SelectedCollection is null)
            return;

        if (IsSaving)
        {
            _pendingSave = true;
            return;
        }

        IsSaving = true;

        try
        {
            do
            {
                _pendingSave = false;
                if (SelectedCollection is UserFontCollection user)
                    user.Fonts = [.. CollectionFonts.Select(c => c.Name)];
                await CollectionService.SaveCollectionAsync(SelectedCollection);
            }
            while (_pendingSave && SelectedCollection is not null);
        }
        finally
        {
            IsSaving = false;
        }
    }

    internal async void ExportAsZip()
    {
        IsExporting = true;

        try
        {
            await ExportManager.ExportCollectionAsZipAsync(
                SelectedCollection,
                p => OnSyncContext(() => CollectionExportProgress = p));
        }
        finally
        {
            IsExporting = false;
        }
    }

    internal async void ExportAsFolder()
    {
        IsExporting = true;

        try
        {
            await ExportManager.ExportCollectionToFolderAsync(
                SelectedCollection,
                p => OnSyncContext(() => CollectionExportProgress = p));
        }
        finally
        {
            IsExporting = false;
        }
    }
}
