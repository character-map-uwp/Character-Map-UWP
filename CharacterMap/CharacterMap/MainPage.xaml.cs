using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using CharacterMap.Core;
using CharacterMap.ViewModel;

namespace CharacterMap
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel MainViewModel { get; set; }

        public AppSettings AppSettings { get; set; }

        public MainPage()
        {
            this.InitializeComponent();
            this.MainViewModel = this.DataContext as MainViewModel;
            this.Loaded += MainPage_Loaded;
            this.NavigationCacheMode = NavigationCacheMode.Required;

            AppSettings = new AppSettings();
            LoadTheme();
        }

        private void LoadTheme()
        {
            this.RequestedTheme = AppSettings.UseDarkThemeSetting ? ElementTheme.Dark : ElementTheme.Light;
            this.ToggleTheme.IsChecked = AppSettings.UseDarkThemeSetting;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
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
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtSelected.Text);
            BorderFadeInStoryboard.Completed += async (o, _) =>
            {
                await Task.Delay(1000);
                BorderFadeOutStoryboard.Begin();
            };
            BorderFadeInStoryboard.Begin();
        }

        /// <summary>
        /// When User Click "Select" Button for an Character in the Grid
        /// </summary>
        private void BtnSelect_OnClick(object sender, RoutedEventArgs e)
        {
            var ch = CharGrid?.SelectedItem as Character;
            if (ch != null)
            {
                if (null != TxtSelected)
                {
                    TxtSelected.Text += ch.Char ?? string.Empty;
                }

                if (null != TxtXamlCode)
                {
                    TxtXamlCode.Text = $"&#x{ch.UnicodeIndex.ToString("x").ToUpper()};";
                }

                var installedFont = LstFontFamily.SelectedItem as InstalledFont;
                if (installedFont != null)
                {
                    TxtFontIcon.Text =
                           $@"<FontIcon FontFamily=""{installedFont.Name}"" Glyph=""&#x{ch.UnicodeIndex.ToString("x").ToUpper()};"" />";
                }
            }
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

            var ch = MainViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
            if (null != ch)
            {
                CharGrid.SelectedItem = ch;
            }
        }

        private async void BtnAbout_OnClick(object sender, RoutedEventArgs e)
        {
            await DigAbout.ShowAsync();
        }

        private async void BtnSavePng_OnClick(object sender, RoutedEventArgs e)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(GridRenderTarget);

            IBuffer buffer = await bitmap.GetPixelsAsync();
            var stream = buffer.AsStream();
            var fileName = $"{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}";
            var result = await Utils.SaveStreamToImage(PickerLocationId.PicturesLibrary, fileName, stream, bitmap.PixelWidth, bitmap.PixelHeight);

            if (result != FileUpdateStatus.Complete)
            {
                var dig = new MessageDialog($"FileUpdateStatus: {result}", "Failed to Save PNG File.");
                await dig.ShowAsync();
            }
        }

        private void BtnCopyXamlCode_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtXamlCode.Text.Trim());
            BorderFadeInStoryboard.Completed += async (o, _) =>
            {
                await Task.Delay(1000);
                BorderFadeOutStoryboard.Begin();
            };
            BorderFadeInStoryboard.Begin();
        }

        private void BtnCopyFontIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtFontIcon.Text.Trim());
            BorderFadeInStoryboard.Completed += async (o, _) =>
            {
                await Task.Delay(1000);
                BorderFadeOutStoryboard.Begin();
            };
            BorderFadeInStoryboard.Begin();
        }

        private void BtnClearCopy_Click(object sender, RoutedEventArgs e)
        {
            TxtSelected.Text = string.Empty;
        }

        private void ToggleTheme_OnChecked(object sender, RoutedEventArgs e)
        {
            if (null != ToggleTheme)
            {
                AppSettings.UseDarkThemeSetting = true;
                LoadTheme();
            }
        }

        private void ToggleTheme_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (null != ToggleTheme)
            {
                AppSettings.UseDarkThemeSetting = false;
                LoadTheme();
            }
        }

        private async void BtnSettings_OnClick(object sender, RoutedEventArgs e)
        {
            await DigSettings.ShowAsync();
        }

        private void BtnSetDefault_OnClick(object sender, RoutedEventArgs e)
        {
            AppSettings.DefaultSelectedFontName = LstFontFamily.SelectedValue as string;
        }
    }
}
