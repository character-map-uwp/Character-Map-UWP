using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using CharacterMap.Annotations;
using CharacterMap.Core;
using CharacterMap.ViewModels;

namespace CharacterMap.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainViewModel ViewModel { get; set; }

        public AppSettings AppSettings { get; set; }

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

            AppSettings = new AppSettings();

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

            if (null != LstFontFamily.Items)
            {
                if (AppSettings.UseDefaultSelection)
                {
                    if (!string.IsNullOrEmpty(AppSettings.DefaultSelectedFontName))
                    {
                        var lastSelectedFont = LstFontFamily.Items.FirstOrDefault(
                        (i =>
                        {
                            var installedFont = i as InstalledFont;
                            return installedFont != null && installedFont.Name == AppSettings.DefaultSelectedFontName;
                        }));

                        if (null != lastSelectedFont)
                        {
                            LstFontFamily.SelectedItem = lastSelectedFont;
                        }
                    }
                }
                else
                {
                    LstFontFamily.SelectedIndex = 0;
                }
            }
        }

        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            var character = CharGrid.SelectedItem as Character;
            if (character != null)
                Edi.UWP.Helpers.Utils.CopyToClipBoard(character.Char);
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
            var unicodeIndex = SearchBoxUnicode.QueryText;
            int intIndex = Utils.ParseHexString(unicodeIndex);

            var ch = ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
            if (null != ch)
            {
                CharGrid.SelectedItem = ch;
            }
        }

        private async void BtnSavePng_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(GridRenderTarget);

                IBuffer buffer = await bitmap.GetPixelsAsync();
                var stream = buffer.AsStream();
                var fileName = $"{DateTime.Now:yyyy-MM-dd-HHmmss}";
                await Utils.SaveStreamToImage(PickerLocationId.PicturesLibrary, fileName, stream, bitmap.PixelWidth, bitmap.PixelHeight);
            }
            catch (Exception ex)
            {
                var dig = new MessageDialog($"{ex.Message}", "Failed to Save PNG File.");
                await dig.ShowAsync();
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

        private void BtnSettings_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void BtnSetDefault_OnClick(object sender, RoutedEventArgs e)
        {
            AppSettings.DefaultSelectedFontName = LstFontFamily.SelectedValue as string;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
