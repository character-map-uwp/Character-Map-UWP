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

        private void SearchBoxUnicode_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var unicodeIndex = SearchBoxUnicode.Text.Trim();
            int intIndex = Utils.ParseHexString(unicodeIndex);
            var ch = FontMap.ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
            if (null != ch)
            {
                FontMap.SelectCharacter(ch);
            }
            else if (ViewModel.SelectedFont.Name == "Segoe MDL2 Assets")    //Search for Segoe MDL2 Assets characters with description
            {
                string descriptionForSearch = SearchBoxUnicode.Text.ToLower().Replace(" ", string.Empty);

                if (MDL2Description.Dict.TryGetValue(descriptionForSearch, out string unicodePoint))
                {
                    //Precise search
                    intIndex = Utils.ParseHexString(unicodePoint);
                    ch = FontMap.ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
                    FontMap.SelectCharacter(ch);
                }
                else
                {
                    //Fuzzy search
                    string resultKey = MDL2Description.Dict.Keys.Where(key => key.Contains(descriptionForSearch)).ToList().FirstOrDefault();
                    if (null != resultKey)
                    {
                        if(MDL2Description.Dict.TryGetValue(resultKey, out string unicodePointFuzzy))
                        {
                            intIndex = Utils.ParseHexString(unicodePointFuzzy);
                            ch = FontMap.ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
                            FontMap.SelectCharacter(ch);
                        }
                    }
                }
            }
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
    }
}
