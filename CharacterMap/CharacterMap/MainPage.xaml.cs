using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using CharacterMap.Core;
using CharacterMap.ViewModel;

namespace CharacterMap
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel MainViewModel { get; set; }
        
        public MainPage()
        {
            this.InitializeComponent();
            this.MainViewModel = this.DataContext as MainViewModel;
            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            CmbFontFamily.SelectedIndex = 0;
        }

        private void CharGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ch = CharGrid?.SelectedItem as Character;
            if (ch != null)
            {
                TxtPreview.Text = ch.Char ?? string.Empty;
                TxtUnicode.Text = "U+" + ch.UnicodeIndex.ToString("x").ToUpper();
            }
        }

        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtSelected.Text);
        }

        private void BtnSelect_OnClick(object sender, RoutedEventArgs e)
        {
            var ch = CharGrid?.SelectedItem as Character;
            if (ch != null)
            {
                TxtSelected.Text += ch.Char ?? string.Empty;

                TxtXamlCode.Text = $"&#x{ch.UnicodeIndex.ToString("x").ToUpper()};";
                var installedFont = CmbFontFamily.SelectedItem as InstalledFont;

                if (installedFont != null)
                    TxtFontIcon.Text =
                        $@"<FontIcon FontFamily=""{installedFont.Name}"" Glyph=""&#x{ch.UnicodeIndex.ToString("x").ToUpper()};"" />";
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
            int intIndex = ParseHexString(unicodeIndex);

            var ch = MainViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == intIndex);
            if (null != ch)
            {
                CharGrid.SelectedItem = ch;
            }
        }

        private static int ParseHexString(string hexNumber)
        {
            hexNumber = hexNumber.Replace("x", string.Empty);
            int result = 0;
            int.TryParse(hexNumber, System.Globalization.NumberStyles.HexNumber, null, out result);
            return result;
        }

        private void BtnAbout_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof (About));
        }

        private async void BtnSavePng_OnClick(object sender, RoutedEventArgs e)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(GridRenderTarget);

            IBuffer buffer = await bitmap.GetPixelsAsync();
            var stream = buffer.AsStream();
            var fileName = $"{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}";
            var result = await SaveStreamToImage(PickerLocationId.PicturesLibrary, fileName, stream, bitmap.PixelWidth, bitmap.PixelHeight);

            if (result != FileUpdateStatus.Complete)
            {
                var dig = new MessageDialog(result.ToString(), "Oh Shit");
                await dig.ShowAsync();
            }
        }

        public async Task<FileUpdateStatus> SaveStreamToImage(PickerLocationId location, string fileName, Stream stream, int pixelWidth, int pixelHeight)
        {
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = location
            };
            savePicker.FileTypeChoices.Add("Png Image", new[] { ".png" });
            savePicker.SuggestedFileName = fileName;
            StorageFile sFile = await savePicker.PickSaveFileAsync();
            if (sFile != null)
            {
                CachedFileManager.DeferUpdates(sFile);

                using (var fileStream = await sFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                    Stream pixelStream = stream;
                    byte[] pixels = new byte[pixelStream.Length];
                    await pixelStream.ReadAsync(pixels, 0, pixels.Length);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                              (uint)pixelWidth,
                              (uint)pixelHeight,
                              96.0,
                              96.0,
                              pixels);
                    await encoder.FlushAsync();
                }

                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(sFile);
                return status;
            }
            return FileUpdateStatus.Failed;
        }

        private void ToggleTheme_OnToggled(object sender, RoutedEventArgs e)
        {
            if (null != ToggleTheme)
            {
                this.RequestedTheme = ToggleTheme.IsOn ? ElementTheme.Dark : ElementTheme.Light;
            }
        }

        private void BtnCopyXamlCode_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtXamlCode.Text.Trim());
        }

        private void BtnCopyFontIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtFontIcon.Text.Trim());
        }

        private void BtnClearCopy_Click(object sender, RoutedEventArgs e)
        {
            TxtSelected.Text = string.Empty;
        }
    }
}
