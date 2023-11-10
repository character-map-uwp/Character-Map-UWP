using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public class CreateCollectionDialogTemplateSettings : ViewModelBase
{
    private string _collectionTitle;
    public string CollectionTitle
    {
        get => _collectionTitle;
        set { if (Set(ref _collectionTitle, value)) OnCollectionTitleChanged(); }
    }

    private bool _isCollectionTitleValid;
    public bool IsCollectionTitleValid
    {
        get => _isCollectionTitleValid;
        private set => Set(ref _isCollectionTitleValid, value);
    }

    private void OnCollectionTitleChanged()
    {
        IsCollectionTitleValid = !string.IsNullOrWhiteSpace(CollectionTitle);
    }
}

public sealed partial class CreateCollectionDialog : ContentDialog
{
    public CreateCollectionDialogTemplateSettings TemplateSettings { get; }

    public bool IsRenameMode { get; }

    public object Result { get; private set; }


    private UserFontCollection _collection = null;

    public CreateCollectionDialog(UserFontCollection collection = null)
    {
        _collection = collection;
        TemplateSettings = new CreateCollectionDialogTemplateSettings();
        this.InitializeComponent();

        if (_collection != null)
        {
            IsRenameMode = true;
            this.Title = Localization.Get("DigRenameCollection/Title");
            this.PrimaryButtonText = Localization.Get("DigRenameCollection/PrimaryButtonText");
            TemplateSettings.CollectionTitle = _collection.Name;
        }
    }


    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var d = args.GetDeferral();

        var collections = Ioc.Default.GetService<UserCollectionsService>();

        if (IsRenameMode)
        {
            this.IsPrimaryButtonEnabled = false;
            this.IsSecondaryButtonEnabled = false;
            InputBox.IsEnabled = false;

            await collections.RenameCollectionAsync(TemplateSettings.CollectionTitle, _collection);
            d.Complete();

            await Task.Yield();
            WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());
        }
        else
        {
            AddToCollectionResult result = null;
            UserFontCollection collection = await collections.CreateCollectionAsync(TemplateSettings.CollectionTitle);

            if (this.DataContext is InstalledFont font)
                result = await collections.AddToCollectionAsync(font, collection);
            else if (this.DataContext is IList<InstalledFont> fonts)
                result = await collections.AddToCollectionAsync(fonts, collection);

            Result = result;
            d.Complete();
            await Task.Yield();
            if (result is not null && result.Success)
                WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, result));

        }
    }
}
