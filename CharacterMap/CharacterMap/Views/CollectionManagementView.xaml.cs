using CharacterMap.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Views;

public interface IActivateableControl
{
    void Activate();
    void Deactivate();
}

public sealed partial class CollectionManagementView : UserControl, IActivateableControl
{
    public CollectionManagementViewModel ViewModel { get; }

    public CollectionManagementView()
    {
        this.InitializeComponent();
        ViewModel = new();
    }

    public void Activate()
    {
        ViewModel.Activate();
    }

    public void Deactivate()
    {
        ViewModel.Deactivate();
    }

    void CollectionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.RefreshFontLists();
    }

    async void NewCollection_Click(object sender, RoutedEventArgs e)
    {
        CreateCollectionDialog d = new();
        d.SetDataContext(null); // Do not remove
        await d.ShowAsync();

        if (d.Result is AddToCollectionResult result && result.Success)
        {
            ViewModel.RefreshCollections();
            SelectCollection(result.Collection);
        }
    }

    void SelectCollection(UserFontCollection collection)
    {
        ViewModel.Activate();
        ViewModel.SelectedCollection = collection;
    }

    async void RenameFontCollection_Click(object sender, RoutedEventArgs e)
    {
        await (new CreateCollectionDialog(ViewModel.SelectedCollection)).ShowAsync();
        SelectCollection(ViewModel.SelectedCollection);
    }

    void DeleteCollection_Click(object sender, RoutedEventArgs e)
    {
        var d = new ContentDialog
        {
            Title = Localization.Get("DigDeleteCollection/Title"),
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = Localization.Get("DigDeleteCollection/PrimaryButtonText"),
            SecondaryButtonText = Localization.Get("DigDeleteCollection/SecondaryButtonText"),
        };

        d.PrimaryButtonClick += DigDeleteCollection_PrimaryButtonClick;
        _ = d.ShowAsync();
    }

    async void DigDeleteCollection_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        string name = ViewModel.SelectedCollection.Name;
        await ViewModel.CollectionService.DeleteCollectionAsync(ViewModel.SelectedCollection);
        CollectionSelector.SelectedItem = null;
        CollectionSelector.SelectedIndex = -1;
        //SelectCollection(null);
        ViewModel.RefreshCollections();
        ViewModel.Messenger.Send(new AppNotificationMessage(true, $"\"{name}\" collection deleted"));
    }

    string GetCountLabel(int fontCount, int selectedCount)
    {
        return string.Format(Localization.Get("FontsSelectedCountLabel"), fontCount, selectedCount);
    }
}
