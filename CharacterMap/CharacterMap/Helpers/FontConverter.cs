using CharacterMap.Core;
using CharacterMap.Models;
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
        private static Random _random { get; } = new Random();

        public static async Task<(StorageFile File, ConversionStatus Result)> TryConvertAsync(
            StorageFile file, StorageFolder targetFolder = null)
        {
            if (file.FileType.ToLower().EndsWith("woff"))
            {
                StorageFolder folder = targetFolder ?? ApplicationData.Current.TemporaryFolder;
                string name = Path.GetFileNameWithoutExtension(file.DisplayName);

                if (targetFolder is not null) // Avoid threading errors with multiple converts to the same target folder
                    name += $"-{_random.Next(1000,100000)}";

                StorageFile newFile = await folder.CreateFileAsync($"{name}.otf", CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
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
            string p = file.Path.Replace(ApplicationData.Current.TemporaryFolder.Path, String.Empty).Replace("\\", "/");
            return new Uri($"ms-appdata:///temp{p}");
        }

        private static Task<ConversionStatus> TryConvertWoffToOtfAsync(StorageFile inputFile, StorageFile outputFile)
        {
            return Task.Run(async () =>
            {
                using var input = await inputFile.OpenStreamForReadAsync().ConfigureAwait(false);
                using var output = await outputFile.OpenStreamForWriteAsync().ConfigureAwait(false);
                var result =  Converter.Convert(input, output);
                return result;
            });
        }

        /// <summary>
        /// Attempts to extract font files from a Zip archive
        /// </summary>
        /// <param name="file">ZIP file</param>
        /// <param name="folder">Folder to extract the contents too</param>
        /// <returns></returns>
        public static async Task<List<StorageFile>> ExtractFontsFromZipAsync(StorageFile file, StorageFolder folder, FolderOpenOptions options)
        {
            List<StorageFile> files = new();

            try
            {
                using var s = await file.OpenStreamForReadAsync().ConfigureAwait(false);
                ZipArchive zip = new(s);

                foreach (var entry in zip.Entries)
                {
                    if (options.IsCancelled)
                        return files;

                    var ext = Path.GetExtension(entry.Name);
                    if (FontFinder.ImportFormats.Contains(ext))
                    {
                        try
                        {
                            var extracted = await entry.ExtractToFolderAsync(folder, entry.Name, CreationCollisionOption.GenerateUniqueName).ConfigureAwait(false);
                            var result = await FontConverter.TryConvertAsync(extracted, folder).ConfigureAwait(false);

                            // If the file was converted we can delete the original extracted file.
                            // We don't need to await this.
                            if (result.File != extracted)
                                _ = extracted.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            if (result.Result == ConversionStatus.OK)
                                files.Add(result.File);
                        }
                        catch
                        {
                            // Possibly file already exists, ExtractToFile doesn't take
                            // options for handling collisions
                        }
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