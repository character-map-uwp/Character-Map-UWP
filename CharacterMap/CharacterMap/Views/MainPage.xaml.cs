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
using CharacterMap.Helpers;
using CharacterMap.ViewModels;

namespace CharacterMap.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainViewModel ViewModel { get; set; }

        private bool _isCtrlKeyPressed;

        private void LayoutRoot_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control) _isCtrlKeyPressed = false;
        }

        private void LayoutRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control) _isCtrlKeyPressed = true;
            else if (_isCtrlKeyPressed)
            {
                switch (e.Key)
                {
                    case VirtualKey.C:
                        if (CharGrid.SelectedItem is Character character)
                            Edi.UWP.Helpers.Utils.CopyToClipBoard(character.Char);
                        BorderFadeInStoryboard.Begin();
                        break;
                }
            }
        }

        private async Task DoRestartRequest()
        {
            await CoreApplication.RequestRestartAsync(string.Empty);
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

        #endregion

        public MainPage()
        {
            this.InitializeComponent();
            this.ViewModel = this.DataContext as MainViewModel;
            this.NavigationCacheMode = NavigationCacheMode.Enabled;

            BorderFadeInStoryboard.Completed += async (o, _) =>
            {
                await Task.Delay(1000);
                BorderFadeOutStoryboard.Begin();
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _coreTitleBar.ExtendViewIntoTitleBar = true;
            Window.Current.SetTitleBar(TitleBarBackgroundElement);
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            _coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;
            Window.Current.SizeChanged += OnWindowSizeChanged;
            UpdateLayoutMetrics();

            if (null != LstFontFamily.SelectedItem)
            {
                LstFontFamily.ScrollIntoView(LstFontFamily.SelectedItem, ScrollIntoViewAlignment.Leading);

                if (null != CharGrid.SelectedItem)
                {
                    CharGrid.ScrollIntoView(CharGrid.SelectedItem, ScrollIntoViewAlignment.Leading);
                }
            }
        }

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

        private void TxtFontIcon_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtFontIcon.SelectAll();
        }

        private void TxtXamlCode_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtXamlCode.SelectAll();
        }

        private void SearchBoxUnicode_OnQuerySubmitted(SearchBox sender, SearchBoxQuerySubmittedEventArgs args)
        {
            var unicodeIndex = SearchBoxUnicode.QueryText.Trim();
            int intIndex = Utils.ParseHexString(unicodeIndex);

            var ch = ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
            if (null != ch)
            {
                CharGrid.SelectedItem = ch;
                CharGrid.ScrollIntoView(ch);
            }
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
    }
}
