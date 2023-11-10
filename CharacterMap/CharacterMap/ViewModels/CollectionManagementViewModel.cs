namespace CharacterMap.ViewModels;

internal partial class CollectionManagementViewModel : ViewModelBase
{
    protected override bool CaptureContext => true;

    #region Properties

    [ObservableProperty] string _collectionExportProgress;
    [ObservableProperty] bool _isSaving = false;
    [ObservableProperty] bool _isExporting = false;

    [ObservableProperty] List<UserFontCollection> _collections;
    [ObservableProperty] ObservableCollection<InstalledFont> _fontList;
    [ObservableProperty] ObservableCollection<InstalledFont> _collectionFonts;

    public ObservableCollection<InstalledFont> SelectedFonts = new();

    public ObservableCollection<InstalledFont> SelectedCollectionFonts = new();
    public UserCollectionsService CollectionService { get; private set; } = null;

    private UserFontCollection _selectedCollection;
    public UserFontCollection SelectedCollection
    {
        get => _selectedCollection;
        set
        {
            if (Set(ref _selectedCollection, value) && value != null)
                RefreshFontLists();
        }
    }

    #endregion




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
        Collections = CollectionService.Items.ToList();
        OnPropertyChanged(nameof(Collections));
    }

    public void RefreshFontLists()
    {
        if (SelectedCollection is null)
        {
            // clear all the things
            CollectionFonts = new();
            FontList = new();
            return;
        }

        // 1. Get list of fonts in and not in the collection
        var collectionFonts = FontFinder.Fonts.Where(f => SelectedCollection.Fonts.Contains(f.Name)).ToList();
        var systemFonts = FontFinder.Fonts.Except(collectionFonts).ToList();

        // 2. Create binding lists
        FontList = new(systemFonts);
        CollectionFonts = new(collectionFonts);
    }

    public void AddToCollection()
    {
        if (SelectedFonts is null || SelectedFonts.Count == 0)
            return;

        var fonts = SelectedFonts.ToList();
        foreach (var font in fonts)
            if (FontList.Remove(font))
                CollectionFonts.AddSorted(font);

        StartSave();
    }

    public void RemoveFromCollection()
    {
        if (SelectedCollectionFonts is null || SelectedCollectionFonts.Count == 0)
            return;

        var fonts = SelectedCollectionFonts.ToList();
        foreach (var font in fonts)
            if (CollectionFonts.Remove(font))
                FontList.AddSorted(font);

        StartSave();
    }

    public void StartSave()
    {
        _ = SaveAsync();
    }

    async Task SaveAsync()
    {
        if (SelectedCollection is null || IsSaving)
            return;

        IsSaving = true;

        try
        {
            SelectedCollection.Fonts = new HashSet<string>(CollectionFonts.Select(c => c.Name));
            await CollectionService.SaveCollectionAsync(SelectedCollection);
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
                CollectionFonts,
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
                CollectionFonts,
                p => OnSyncContext(() => CollectionExportProgress = p));
        }
        finally
        {
            IsExporting = false;
        }
    }
}
