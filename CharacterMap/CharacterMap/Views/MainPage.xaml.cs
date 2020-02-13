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

namespace CharacterMap.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainViewModel ViewModel { get; }

        public AppSettings Settings { get; }

        private bool _isCtrlKeyPressed;

        public MainPage()
        {
            InitializeComponent();
            Settings = (AppSettings)App.Current.Resources[nameof(AppSettings)];

            ViewModel = DataContext as MainViewModel;
            NavigationCacheMode = NavigationCacheMode.Enabled;

            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
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
                        await FontMapView.CreateNewViewForFontAsync(font);
                    }
                }
                finally
                {
                    ViewModel.IsLoadingFonts = false;
                }
            }
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
            if (sender is MenuFlyout menu)
            {
                // Reset to default menu
                while (menu.Items.Count > 7)
                    menu.Items.RemoveAt(7);

                // force menu width to match the source button
                menu.Items.OfType<MenuFlyoutSeparator>().Last().Width = FontListFilter.ActualWidth;

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
                                ViewModel.SelectedCollection = u;
                        };
                        menu.Items.Add(m);
                    }
                }
            }
        }

        private void FontContextFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout menu && menu.Target.DataContext is InstalledFont font)
            {
                MenuFlyoutSubItem coll = menu.Items.LastOrDefault(m => m is MenuFlyoutSubItem) as MenuFlyoutSubItem;

                while (coll.Items.Count > 4)
                    coll.Items.RemoveAt(4);

                if (coll.Items.FirstOrDefault(i => i.Name == "SymbolFontItem") is MenuFlyoutItemBase fontItem)
                {
                    if (font.IsSymbolFont)
                    {
                        fontItem.Visibility = Visibility.Collapsed;
                        coll.Items[1].Visibility = Visibility.Collapsed; // Remove the related seperator
                    }
                    else
                    {
                        fontItem.IsEnabled = !ViewModel.FontCollections.SymbolCollection.Fonts.Contains(font.Name);
                    }
                }

                if (menu.Items.FirstOrDefault(i => i.Name == "RemoveFromCollectionItem") is MenuFlyoutItemBase b)
                {
                    b.Visibility = ViewModel.SelectedCollection == null ? Visibility.Collapsed : Visibility.Visible;
                }

                if (ViewModel.FontCollections.Items.Count > 0)
                {
                    foreach (var item in ViewModel.FontCollections.Items)
                    {
                        var m = new MenuFlyoutItem { DataContext = item, Text = item.Name, IsEnabled = !item.Fonts.Contains(font.Name) };
                        if (m.IsEnabled)
                        {
                            m.Click += async (s, a) =>
                            {
                                await ViewModel.FontCollections.AddToCollectionAsync(
                                    font, (UserFontCollection)(((FrameworkElement)s).DataContext));
                            };
                        }
                        coll.Items.Add(m);
                    }
                }
            }
        }

        private void AddToSymbolFonts_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is InstalledFont font)
            {
                _ = ViewModel.FontCollections.AddToCollectionAsync(
                               font, ViewModel.FontCollections.SymbolCollection);
            }
        }

        private async void RemoveFromCollectionItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is InstalledFont font)
            {
                await ViewModel.FontCollections.RemoveFontCollectionAsync(font, ViewModel.SelectedCollection);
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
    }
}
