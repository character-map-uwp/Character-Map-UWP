using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI;

namespace CharacterMap.Core
{
    public class Utils
    {
        public static void UseDarkTheme()
        {
            Edi.UWP.Helpers.UI.ApplyColorToTitleBar(Color.FromArgb(255, 43, 43, 43), Colors.White, Colors.DimGray, Colors.White);
            Edi.UWP.Helpers.UI.ApplyColorToTitleButton(Color.FromArgb(255, 43, 43, 43), Colors.White, Colors.DimGray, Colors.White, Colors.DimGray, Colors.White, Colors.DimGray, Colors.White);
            Edi.UWP.Helpers.Mobile.SetWindowsMobileStatusBarColor(Color.FromArgb(255, 43, 43, 43), Colors.DarkGray);
        }

        public static void UseLightTheme()
        {
            var accentColor = Edi.UWP.Helpers.UI.GetAccentColor();
            var btnHoverColor = Color.FromArgb(128,
                (byte)(accentColor.R + 30),
                (byte)(accentColor.G + 30),
                (byte)(accentColor.B + 30));

            Edi.UWP.Helpers.UI.ApplyColorToTitleBar(
            accentColor,
            Colors.White,
            Colors.LightGray,
            Colors.Gray);

            Edi.UWP.Helpers.UI.ApplyColorToTitleButton(
                accentColor, Colors.White,
                btnHoverColor, Colors.White,
                accentColor, Colors.White,
                Colors.LightGray, Colors.Gray);
        }
        public static int ParseHexString(string hexNumber)
        {
            hexNumber = hexNumber.Replace("x", string.Empty);
            int result = 0;
            int.TryParse(hexNumber, System.Globalization.NumberStyles.HexNumber, null, out result);
            return result;
        }

        public static async Task<FileUpdateStatus> SaveStreamToImage(PickerLocationId location, string fileName, Stream stream, int pixelWidth, int pixelHeight)
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
                    var localDpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                    Stream pixelStream = stream;
                    byte[] pixels = new byte[pixelStream.Length];
                    await pixelStream.ReadAsync(pixels, 0, pixels.Length);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                              (uint)pixelWidth,
                              (uint)pixelHeight,
                              localDpi,
                              localDpi,
                              pixels);
                    await encoder.FlushAsync();
                }

                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(sFile);
                return status;
            }
            return FileUpdateStatus.Failed;
        }
    }
}
