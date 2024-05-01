using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public partial class CreateCollectionDialogTemplateSettings : ViewModelBase
{
    private string _collectionTitle;
    public string CollectionTitle
    {
        get => _collectionTitle;
        set { if (Set(ref _collectionTitle, value)) OnCollectionTitleChanged(); }
    }

    [ObservableProperty] bool _isCollectionTitleValid;

    [ObservableProperty] bool _isSmartCollection;
    [ObservableProperty] string _filterFilePath;
    [ObservableProperty] string _filterFoundry;
    [ObservableProperty] string _filterDesigner;


    private void OnCollectionTitleChanged()
    {
        IsCollectionTitleValid = !string.IsNullOrWhiteSpace(CollectionTitle);
    }

    public void Populate(IFontCollection c)
    {
        CollectionTitle = c.Name;

        if (c is SmartFontCollection u)
        {
            IsSmartCollection = true;

            Populate("filepath:", s => FilterFilePath = s);
            Populate("foundry:", s => FilterFoundry = s);
            Populate("designer:", s => FilterDesigner = s);

            void Populate(string id, Action<string> set)
            {
                foreach (var s in u.Filters)
                {
                    if (s.StartsWith(id))
                        set(s.Replace(id, string.Empty).Trim());
                }
            }
        }
    }

    public List<String> GetFilterList()
    {
        List<String> filters = [];
        Add("filepath:", _filterFilePath);
        Add("foundry:", _filterFoundry);
        Add("designer:", _filterDesigner);
        return filters;

        void Add(string id, string field)
        {
            if (!String.IsNullOrWhiteSpace(field))
                filters.Add($"{id} {field.Replace(id, string.Empty).Trim()}");
        }
    }
}

public sealed partial class CreateCollectionDialog : ContentDialog
{
    public CreateCollectionDialogTemplateSettings TemplateSettings { get; }

    public bool IsEditMode { get; }

    public object Result { get; private set; }


    private IFontCollection _collection = null;

    public CreateCollectionDialog(IFontCollection collection = null)
    {
        _collection = collection;
        TemplateSettings = new CreateCollectionDialogTemplateSettings();
        this.InitializeComponent();

        if (_collection != null)
        {
            IsEditMode = true;
            this.Title = Localization.Get("DigRenameCollection/Title");
            this.PrimaryButtonText = Localization.Get("DigRenameCollection/PrimaryButtonText");
            TemplateSettings.Populate(_collection);
        }
    }


    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var d = args.GetDeferral();

        var collections = Ioc.Default.GetService<UserCollectionsService>();

        if (IsEditMode)
        {
            this.IsPrimaryButtonEnabled = false;
            this.IsSecondaryButtonEnabled = false;
            InputBox.IsEnabled = false;

            _collection.Name = TemplateSettings.CollectionTitle;

            if (_collection is SmartFontCollection s)
                s.Filters = TemplateSettings.GetFilterList();

            await collections.UpdateCollectionAsync(_collection);
            d.Complete();

            await Task.Yield();
            WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());
        }
        else
        {
            AddToCollectionResult result = null;

            // Check if we are creating a Smart Filter
            if (TemplateSettings.GetFilterList() is { Count: > 0} filters)
            {
                SmartFontCollection collection = await collections.CreateSmartCollectionAsync(
                    TemplateSettings.CollectionTitle,
                    filters);

                d.Complete();
            }
            else
            {
                UserFontCollection collection = await collections.CreateCollectionAsync(TemplateSettings.CollectionTitle);

                if (this.DataContext is InstalledFont font)
                    result = await collections.AddToCollectionAsync(font, collection);
                else if (this.DataContext is IReadOnlyList<InstalledFont> fonts)
                    result = await collections.AddToCollectionAsync(fonts, collection);
                else if (this.DataContext is null)
                    result = new AddToCollectionResult(true, null, collection);

                Result = result;
                d.Complete();
                await Task.Yield();
                if (result is not null && result.Success && result.Fonts is not null)
                    WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, result));

            }
        }
    }
}
