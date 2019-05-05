using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using CharacterMap.Annotations;
using CharacterMap.Core;
using CharacterMap.ViewModels;
using System.Diagnostics;
using CharacterMap.Helpers;
using Windows.UI.Xaml.Media;
using GalaSoft.MvvmLight.Messaging;
using Windows.Storage;
using System.Collections.Generic;
using CharacterMap.Services;
using Windows.Storage.Pickers;

namespace CharacterMap.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainViewModel ViewModel { get; }

        public AppSettings Settings { get; }

        private bool _isCtrlKeyPressed;

        public MainPage()
        {
            this.InitializeComponent();
            Settings = (AppSettings)App.Current.Resources[nameof(AppSettings)];

            this.ViewModel = this.DataContext as MainViewModel;
            this.NavigationCacheMode = NavigationCacheMode.Enabled;

            this.Loaded += MainPage_Loaded;
            this.Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.ViewModel.FontListCreated -= ViewModel_FontListCreated;
            this.ViewModel.FontListCreated += ViewModel_FontListCreated;
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
            this.ViewModel.FontListCreated -= ViewModel_FontListCreated;
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
                && font.HasImportedFiles
                && grid.ContextFlyout != null)
            {
                grid.ContextFlyout.ShowAt(grid);
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
    }
}
