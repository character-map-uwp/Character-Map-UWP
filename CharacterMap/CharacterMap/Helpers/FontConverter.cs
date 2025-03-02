using System.IO.Compression;
using WoffToOtf;

namespace CharacterMap.Helpers;

public static class FontConverter
{
    private static Random _random { get; } = new();

    public static async Task<(StorageFile File, ConversionStatus Result)> TryConvertAsync(
        StorageFile file, StorageFolder targetFolder = null)
    {
        bool isWoff = file.FileType.ToLower().EndsWith("woff");
        bool isWoff2 = file.FileType.ToLower().EndsWith("woff2");

        if (isWoff || isWoff2)
        {
            StorageFolder folder = targetFolder ?? ApplicationData.Current.TemporaryFolder;
            string name = Path.GetFileNameWithoutExtension(file.DisplayName);

            if (targetFolder is not null) // Avoid threading errors with multiple converts to the same target folder
                name += $"-{_random.Next(1000, 100000)}";

            StorageFile newFile = await folder.CreateFileAsync($"{name}.otf", CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
            ConversionStatus result =
                isWoff ? await TryConvertWoffToOtfAsync(file, newFile).ConfigureAwait(false)
                : await TryConvertToWoff2Async(file, newFile).ConfigureAwait(false);

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

        return (file, ConversionStatus.OK);
    }

    public static async Task<ConversionStatus> TryConvertToWoff2Async(StorageFile file, StorageFile newFile)
    {
        try
        {
            IBuffer buffer = null;
            using (var stream = await file.OpenReadAsync())
            using (DataReader reader = new(stream))
            {
                await reader.LoadAsync((uint)stream.Size);
                buffer = reader.ReadBuffer(reader.UnconsumedBufferLength);
            }

            using (var os = await newFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                await Utils.GetInterop().UnpackWOFF2Async(buffer, os);
            }

            return ConversionStatus.OK;
        }
        catch (Exception ex)
        {
            return ConversionStatus.UnspecifiedError;
        }
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
            var result = Converter.Convert(input, output);
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
                if (FontImporter.ImportFormats.Contains(ext.ToLower()))
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