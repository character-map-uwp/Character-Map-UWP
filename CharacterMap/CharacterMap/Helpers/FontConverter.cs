using CharacterMap.Core;
using CharacterMapCX;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.Storage;
using WoffToOtf;

namespace CharacterMap.Helpers
{
    public static class FontConverter
    {
        public static async Task<(StorageFile File, ConversionStatus Result)> TryConvertAsync(
            StorageFile file, StorageFolder targetFolder = null)
        {
            if (file.FileType.ToLower().EndsWith("woff"))
            {
                var folder = targetFolder ?? ApplicationData.Current.TemporaryFolder;
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

        /// <summary>
        /// Attempts to extract font files from a Zip archive
        /// </summary>
        /// <param name="file">ZIP file</param>
        /// <param name="folder">Folder to extract the contents too</param>
        /// <returns></returns>
        public static async Task<List<StorageFile>> ExtractFontsFromZipAsync(StorageFile file, StorageFolder folder)
        {
            List<StorageFile> files = new();

            try
            {
                using var s = await file.OpenStreamForReadAsync().ConfigureAwait(false);
                ZipArchive zip = new(s);

                foreach (var entry in zip.Entries)
                {
                    var ext = Path.GetExtension(entry.Name);
                    if (FontFinder.ImportFormats.Contains(ext))
                    {
                        string dest = Path.Combine(folder.Path, entry.Name);
                        entry.ExtractToFile(dest, true);

                        var extracted = await StorageFile.GetFileFromPathAsync(dest);
                        var result = await FontConverter.TryConvertAsync(extracted, folder);

                        // If the file was converted we can delete the original extracted file.
                        // We don't need to await this.
                        if (result.File != extracted)
                            _ = extracted.DeleteAsync(StorageDeleteOption.PermanentDelete);

                        if (result.Result == ConversionStatus.OK)
                            files.Add(result.File);
                    }
                }
            }
            catch
            {
                // Possible causes:
                //  - A corrupt Zip
                //  - The file wasn't actually a real Zip
                //  - We've run out of storage space to extract too
            }

            return files;
        }
    }
}