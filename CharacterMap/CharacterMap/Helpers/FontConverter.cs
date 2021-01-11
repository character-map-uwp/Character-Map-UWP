using CharacterMap.Core;
using CharacterMapCX;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace CharacterMap.Helpers
{
    public static class FontConverter
    {
        public static async Task<StorageFile> TryConvertAsync(StorageFile file)
        {
            if (file.FileType.ToLower().EndsWith("woff"))
            {
                var folder = ApplicationData.Current.TemporaryFolder;
                var newFile = await folder.CreateFileAsync(file.DisplayName + ".otf", CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                await TryConvertWoffToOtfAsync(file, newFile).ConfigureAwait(false);

                if (DirectWrite.HasValidFonts(GetAppUri(newFile)))
                    return newFile;
                else
                {
                    await newFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
                    return null;
                }
            }

            return file;
        }

        static Uri GetAppUri(StorageFile file)
        {
            return new Uri($"ms-appdata:///temp/{file.Name}");
        }

        private static Task<bool> TryConvertWoffToOtfAsync(StorageFile inputFile, StorageFile outputFile)
        {
            return Task.Run(async () =>
            {
                using var input = await inputFile.OpenReadAsync();
                using var output = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
                using var si = input.AsStream();
                using var so = output.AsStream();
                WoffToOtf.Converter.Convert(si, so);

                return true;
            });
        }
    }
}