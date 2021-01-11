using CharacterMap.Core;
using CharacterMapCX;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using WoffToOtf;

namespace CharacterMap.Helpers
{
    public static class FontConverter
    {
        public static async Task<(StorageFile File, ConversionStatus Result)> TryConvertAsync(StorageFile file)
        {
            if (file.FileType.ToLower().EndsWith("woff"))
            {
                var folder = ApplicationData.Current.TemporaryFolder;
                var newFile = await folder.CreateFileAsync(file.DisplayName + ".otf", CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                ConversionStatus result = await TryConvertWoffToOtfAsync(file, newFile).ConfigureAwait(false);
                if (result == ConversionStatus.OK)
                {
                    if (DirectWrite.HasValidFonts(GetAppUri(newFile)))
                        return (newFile, ConversionStatus.OK);
                    else
                        return (default, ConversionStatus.UnspecifiedError);
                }
                else
                {
                    await newFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
                    return (default, result);
                }
            }

            if (file.FileType.ToLower().EndsWith("woff2"))
                return (default, ConversionStatus.UnsupportedWOFF2);

            return (file, ConversionStatus.OK);
        }

        static Uri GetAppUri(StorageFile file)
        {
            return new Uri($"ms-appdata:///temp/{file.Name}");
        }

        private static Task<ConversionStatus> TryConvertWoffToOtfAsync(StorageFile inputFile, StorageFile outputFile)
        {
            return Task.Run(async () =>
            {
                using var input = await inputFile.OpenReadAsync();
                using var output = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
                using var si = input.AsStream();
                using var so = output.AsStream();
                return Converter.Convert(si, so);
            });
        }
    }
}