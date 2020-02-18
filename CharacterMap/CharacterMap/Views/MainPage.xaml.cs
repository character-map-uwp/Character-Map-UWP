using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using CharacterMap.Annotations;
using CharacterMap.Core;
using CharacterMap.ViewModels;
using CharacterMap.Helpers;
using Windows.Storage.Pickers;
using CharacterMap.Services;
using GalaSoft.MvvmLight.Messaging;
using System.Collections.Generic;

namespace CharacterMap.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainViewModel ViewModel { get; }

        public AppSettings Settings { get; }

        private bool _isCtrlKeyPressed;

        public static CoreDispatcher MainDispatcher { get; private set; }

        private Debouncer _fontListDebouncer { get; } = new Debouncer();

        public MainPage()
        {
            InitializeComponent();
            Settings = (AppSettings)App.Current.Resources[nameof(AppSettings)];

            ViewModel = DataContext as MainViewModel;
            NavigationCacheMode = NavigationCacheMode.Enabled;

            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;

            MainDispatcher = Dispatcher;
            Messenger.Default.Register<CollectionsUpdatedMessage>(this, OnCollectionsUpdated);
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.FontListCreated -= ViewModel_FontListCreated;
            ViewModel.FontListCreated += ViewModel_FontListCreated;
        }

        private void ViewModel_FontListCreated(object sender, EventArgs e)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                await Task.Delay(50);
                LstFontFamily.ScrollIntoView(
                    LstFontFamily.SelectedItem, ScrollIntoViewAlignment.Leading);
            });
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.FontListCreated -= ViewModel_FontListCreated;
        }



        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void BtnSettings_OnClick(object sender, RoutedEventArgs e)
        {
            await DigSettings.ShowAsync();
        }

        private async void BtnRestart_OnClick(object sender, RoutedEventArgs e)
        {
            await DoRestartRequest();
        }

        private async Task DoRestartRequest()
        {
            await CoreApplication.RequestRestartAsync(string.Empty);
        }

        private void LayoutRoot_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control) _isCtrlKeyPressed = false;
        }

        private void LayoutRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control)
            {
                _isCtrlKeyPressed = true;
                return;
            }

            if (_isCtrlKeyPressed)
            {
                switch (e.Key)
                {
                    case VirtualKey.C:
                        FontMap.TryCopy();
                        break;
                }
            }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                ViewModel.IsLoadingFonts = true;
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (await FontFinder.ImportFontsAsync(items) is FontImportResult result
                        && result.Imported.Count > 0)
                    {
                        ViewModel.RefreshFontList();
                        ViewModel.TrySetSelectionFromImport(result);
                    }
                }
                finally
                {
                    ViewModel.IsLoadingFonts = false;
                }
            }
        }

        private void Grid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is Grid grid
                && grid.DataContext is InstalledFont font
                && font.HasImportedFiles)
            {
                grid.ContextFlyout?.ShowAt(grid);
            }
        }

        private void RemoveMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is InstalledFont font)
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ViewModel.TryRemoveFont(font);
                });
            }
        }

        private void RemoveMenuFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout flyout 
                && flyout.Items[0].Tag is InstalledFont font
                && font.HasImportedFiles == false)
            {
                flyout.Hide();
            }
        }

        private void OpenInWindowFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item 
                && item.DataContext is InstalledFont font)
            {
                _ = FontMapView.CreateNewViewForFontAsync(font);
            }
        }

        void OnCollectionsUpdated(CollectionsUpdatedMessage msg)
        {
            if (ViewModel.InitialLoad.IsCompleted)
            {
                if (Dispatcher.HasThreadAccess)
                    ViewModel.RefreshFontList(ViewModel.SelectedCollection);
                else
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        ViewModel.RefreshFontList(ViewModel.SelectedCollection);
                    });
                }
            }
        }

        private async void PickFonts()
        {
            var picker = new FileOpenPicker();
            foreach (var format in FontFinder.SupportedFormats)
                picker.FileTypeFilter.Add(format);

            picker.CommitButtonText = Localization.Get("FilePickerConfirm");
            var files = await picker.PickMultipleFilesAsync();
            if (files.Any())
            {
                ViewModel.IsLoadingFonts = true;
                try
                {
                    if (await FontFinder.ImportFontsAsync(files.ToList()) is FontImportResult result
                        && result.Imported.Count > 0)
                    {
                        ViewModel.RefreshFontList();
                        ViewModel.TrySetSelectionFromImport(result);
                    }
                }
                finally
                {
                    ViewModel.IsLoadingFonts = false;
                }
            }
        }

        private async void OpenFont()
        {
            var picker = new FileOpenPicker();
            foreach (var format in FontFinder.SupportedFormats)
                picker.FileTypeFilter.Add(format);

            picker.CommitButtonText = Localization.Get("OpenFontPickerConfirm");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    ViewModel.IsLoadingFonts = true;

                    if (await FontFinder.LoadFromFileAsync(file) is InstalledFont font)
                    {
                        await FontMapView.CreateNewViewForFontAsync(font, file);
                    }
                }
                finally
                {
                    ViewModel.IsLoadingFonts = false;
                }
            }
        }

        private string UpdateFontCountLabel(List<InstalledFont> fontList)
        {
            if (fontList != null)
                return Localization.Get("StatusBarFontCount", fontList.Count);

            return string.Empty;
        }

        public string UpdateCharacterCountLabel(FontVariant variant)
        {
            if (variant != null)
                return Localization.Get("StatusBarCharacterCount", variant.Characters.Count);

            return string.Empty;
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            FontMap.SearchBox_SuggestionChosen(sender, args);
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            FontMap.OnSearchBoxGotFocus(SearchBox);
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                e.Handled = true;
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            FontMap.OnSearchBoxSubmittedQuery(SearchBox);
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            // Handles forming the flyout when opening the main FontFilter 
            // drop down menu.
            if (sender is MenuFlyout menu)
            {
                // Reset to default menu
                while (menu.Items.Count > 7)
                    menu.Items.RemoveAt(7);

                // force menu width to match the source button
                foreach (var sep in menu.Items.OfType<MenuFlyoutSeparator>())
                    sep.MinWidth = FontListFilter.ActualWidth;

                // add users collections 
                if (ViewModel.FontCollections.Items.Count > 0)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());
                    foreach (var item in ViewModel.FontCollections.Items)
                    {
                        var m = new MenuFlyoutItem { DataContext = item, Text = item.Name };
                        m.Click += (s, a) =>
                        {
                            if (m.DataContext is UserFontCollection u)
                            {
                                if (!FontsSemanticZoom.IsZoomedInViewActive)
                                    FontsSemanticZoom.IsZoomedInViewActive = true;

                                ViewModel.SelectedCollection = u;
                            }
                        };
                        menu.Items.Add(m);
                    }
                }

                if (!FontFinder.HasAppxFonts && !FontFinder.HasRemoteFonts)
                {
                    FontSourceSeperator.Visibility = CloudFontsOption.Visibility = AppxOption.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FontSourceSeperator.Visibility = Visibility.Visible;
                    CloudFontsOption.Visibility = FontFinder.HasRemoteFonts ? Visibility.Visible : Visibility.Collapsed;
                    AppxOption.Visibility = FontFinder.HasAppxFonts ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void FontContextFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout menu && menu.Target.DataContext is InstalledFont font)
            {
                FlyoutHelper.CreateMenu(
                    menu, 
                    font, 
                    false,
                    DigCreateCollection);
            }
        }

        private async void AddToSymbolFonts_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is InstalledFont font)
            {
                await ViewModel.FontCollections.AddToCollectionAsync(
                               font, ViewModel.FontCollections.SymbolCollection);

                ViewModel.RefreshFontList(ViewModel.SelectedCollection);
            }
        }

        private async void RemoveFromCollectionItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is InstalledFont font)
            {
                UserFontCollection collection = (ViewModel.SelectedCollection == null && ViewModel.FontListFilter == 1)
                    ? ViewModel.FontCollections.SymbolCollection
                    : ViewModel.SelectedCollection;

                await ViewModel.FontCollections.RemoveFromCollectionAsync(font, collection);
                ViewModel.RefreshFontList(ViewModel.SelectedCollection);
            }
        }

        private void CreateFontCollection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CollectionTitle = null;
            DigCreateCollection.DataContext = (sender as FrameworkElement)?.DataContext;
            _ = DigCreateCollection.ShowAsync();
        }

        private void RenameFontCollection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CollectionTitle = ViewModel.SelectedCollection.Name;
            _ = DigRenameCollection.ShowAsync();
        }

        private void SetFilter(object filter)
        {
            ViewModel.FontListFilter = (int)filter;
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f)
            {
                if (!FontsSemanticZoom.IsZoomedInViewActive)
                    FontsSemanticZoom.IsZoomedInViewActive = true;

                var filter = Convert.ToInt32(f.Tag.ToString(), 10);
                if (filter == ViewModel.FontListFilter)
                    ViewModel.RefreshFontList();
                else
                    ViewModel.FontListFilter = filter;
            }
        }

        private void DeleteCollection_Click(object sender, RoutedEventArgs e)
        {
            _ = DigDeleteCollection.ShowAsync();
        }

        private async void DigCreateCollection_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var d = args.GetDeferral();
            var collection = await ViewModel.FontCollections.CreateCollectionAsync(ViewModel.CollectionTitle);
            await ViewModel.FontCollections.AddToCollectionAsync(sender.DataContext as InstalledFont, collection);
            d.Complete();
        }

        private async void DigRenameCollection_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var d = args.GetDeferral();
            await ViewModel.FontCollections.RenameCollectionAsync(ViewModel.CollectionTitle, ViewModel.SelectedCollection);
            ViewModel.RefreshFontList(ViewModel.SelectedCollection);
            d.Complete();
        }

        private void DigRenameCollection_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            sender.Hide();
        }

        private async void DigDeleteCollection_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            await ViewModel.FontCollections.DeleteCollectionAsync(ViewModel.SelectedCollection);
            ViewModel.RefreshFontList();
        }

        private void DigDeleteCollection_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            sender.Hide();
        }

        private void FontListDisplayToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (ViewModel.InitialLoad.IsCompleted)
            {
                _fontListDebouncer.Debounce(16, () =>
                {
                    ViewModel.RefreshFontList(ViewModel.SelectedCollection);
                });
            }
        }
    }
}
