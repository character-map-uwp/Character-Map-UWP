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

namespace CharacterMap.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainViewModel ViewModel { get; set; }

        private bool _isCtrlKeyPressed;

        public MainPage()
        {
            this.InitializeComponent();

            SetTitleBar();

            this.ViewModel = this.DataContext as MainViewModel;
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (null != LstFontFamily.SelectedItem)
            {
                LstFontFamily.ScrollIntoView(LstFontFamily.SelectedItem, ScrollIntoViewAlignment.Leading);

                if (null != CharGrid.SelectedItem)
                {
                    CharGrid.ScrollIntoView(CharGrid.SelectedItem, ScrollIntoViewAlignment.Leading);
                }
            }
        }

        #region Title Bar

        private CoreApplicationViewTitleBar _coreTitleBar = CoreApplication.GetCurrentView().TitleBar;

        public Thickness CoreTitleBarPadding
        {
            get
            {
                if (ApplicationView.GetForCurrentView().IsFullScreenMode)
                {
                    return new Thickness(0, 0, 0, 0);
                }
                return FlowDirection == FlowDirection.LeftToRight ?
                    new Thickness { Left = _coreTitleBar.SystemOverlayLeftInset, Right = _coreTitleBar.SystemOverlayRightInset } :
                    new Thickness { Left = _coreTitleBar.SystemOverlayRightInset, Right = _coreTitleBar.SystemOverlayLeftInset };
            }
        }

        public double CoreTitleBarHeight => _coreTitleBar.Height;


        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            UpdateLayoutMetrics();
        }

        void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object e)
        {
            UpdateLayoutMetrics();
        }

        void UpdateLayoutMetrics()
        {
            OnPropertyChanged(nameof(CoreTitleBarHeight));
            OnPropertyChanged(nameof(CoreTitleBarPadding));
        }

        private void SetTitleBar()
        {
            Window.Current.SetTitleBar(TitleBarBackgroundElement);

            _coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;
            Window.Current.SizeChanged += OnWindowSizeChanged;
            UpdateLayoutMetrics();
        }

        #endregion

        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            if (CharGrid.SelectedItem is Character character)
            {
                var dp = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy,
                };
                dp.SetText(character.Char);
                Clipboard.SetContent(dp);
            }
            BorderFadeInStoryboard.Begin();
        }

        private void BtnSaveAs_OnClick(object sender, RoutedEventArgs e)
        {
            SaveAsCommandBar.IsOpen = !SaveAsCommandBar.IsOpen;
        }

        private void BtnSaveAsSvg_OnClick(object sender, RoutedEventArgs e)
        {
            SaveAsSvgCommandBar.IsOpen = !SaveAsSvgCommandBar.IsOpen;
        }

        private void TxtFontIcon_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtFontIcon.SelectAll();
        }

        private void TxtXamlCode_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtXamlCode.SelectAll();
        }

        private void BtnCopyXamlCode_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtXamlCode.Text.Trim());
            BorderFadeInStoryboard.Begin();
        }

        private void BtnCopyFontIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtFontIcon.Text.Trim());
            BorderFadeInStoryboard.Begin();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TxtSymbolIcon_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtSymbolIcon.SelectAll();
        }

        private void BtnCopySymbolIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtSymbolIcon.Text.Trim());
            BorderFadeInStoryboard.Begin();
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
            var ch = ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
            if (null != ch)
            {
                CharGrid.SelectedItem = ch;
                CharGrid.ScrollIntoView(ch);
            }
            else if (ViewModel.SelectedFont.Name == "Segoe MDL2 Assets")    //Search for Segoe MDL2 Assets characters with description
            {
                string descriptionForSearch = SearchBoxUnicode.Text.ToLower().Replace(" ", string.Empty);

                if (MDL2Description.Dict.TryGetValue(descriptionForSearch, out string unicodePoint))
                {
                    //Precise search
                    intIndex = Utils.ParseHexString(unicodePoint);
                    ch = ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
                    if (null != ch)
                    {
                        CharGrid.SelectedItem = ch;
                        CharGrid.ScrollIntoView(ch);
                    }
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
                            ch = ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
                            if (null != ch)
                            {
                                CharGrid.SelectedItem = ch;
                                CharGrid.ScrollIntoView(ch);
                            }
                        }
                    }
                }
            }
        }

        private void PreviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var newSize = e.NewSize.Width - 2;

            foreach (AppBarButton item in SaveAsCommandBar.SecondaryCommands.Concat(SaveAsSvgCommandBar.SecondaryCommands))
            {
                item.Width = newSize;
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
                        if (CharGrid.SelectedItem is Character character &&
                            !TxtSymbolIcon.SelectedText.Any() &&
                            !TxtFontIcon.SelectedText.Any() &&
                            !TxtXamlCode.SelectedText.Any())
                        {
                            Edi.UWP.Helpers.Utils.CopyToClipBoard(character.Char);
                            BorderFadeInStoryboard.Begin();
                        }

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
                var items = await e.DataView.GetStorageItemsAsync();
                if (await FontFinder.ImportFontsAsync(items) is FontImportResult result
                    && result.Imported.Count > 0)
                {
                    ViewModel.RefreshFontList();
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
    }
}
