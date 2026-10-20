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
            StorageFolder folder = targetFolder ?? await StorageHelper.CreateTempFolderAsync("CV", CreationCollisionOption.OpenIfExists);
            string name = Path.GetFileNameWithoutExtension(file.DisplayName);

            if (targetFolder is not null) // Avoid threading errors with multiple converts to the same target folder
                name += $"-{_random.Next(1000, 100000)}";

            StorageFile newFile = await folder.CreateFileAsync($"{name}.otf", CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
            ConversionStatus result = isWoff 
                ? await TryConvertWoffToOtfAsync(file, newFile).ConfigureAwait(false)
                : await TryConvertToWoff2Async(file, newFile).ConfigureAwait(false);

            if (result == ConversionStatus.OK)
            {
                if (DirectWrite.HasValidFonts(newFile))
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
            Utils.AppendDiagnostics("FontConverter TryConvertToWoff2Async", ex);
            return ConversionStatus.UnspecifiedError;
        }
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
        List<StorageFile> files = [];

        try
        {
            using Stream s = await file.OpenStreamForReadAsync().ConfigureAwait(false);
            using ZipArchive zip = new(s, ZipArchiveMode.Read);
            string folderPath = folder.Path;

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (options.IsCancelled)
                    return files;

                string ext = Path.GetExtension(entry.Name);
                if (FontImporter.ImportFormats.Contains(ext.ToLower()))
                {
                    try
                    {
                        bool isWoff = ext.EndsWith("woff", StringComparison.OrdinalIgnoreCase);
                        bool isWoff2 = ext.EndsWith("woff2", StringComparison.OrdinalIgnoreCase);

                        if (isWoff || isWoff2)
                        {
                            StorageFile extracted = await entry.ExtractToFolderAsync(folder, entry.Name, CreationCollisionOption.GenerateUniqueName).ConfigureAwait(false);
                            (StorageFile File, ConversionStatus Result) result = await FontConverter.TryConvertAsync(extracted, folder).ConfigureAwait(false);

                            // If the file was converted we can delete the original extracted file.
                            if (result.File != extracted)
                                await extracted.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);

                            if (result.Result == ConversionStatus.OK)
                                files.Add(result.File);
                        }
                        else
                        {
                            string fileName = $"{Path.GetRandomFileName()}{ext}";
                            string targetPath = Path.Combine(folderPath, fileName);
                            using (Stream entryStream = entry.Open())
                            using (FileStream fs = new(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                                entryStream.CopyTo(fs);

                            StorageFile extracted = await StorageFile.GetFileFromPathAsync(targetPath).AsTask().ConfigureAwait(false);
                            files.Add(extracted);
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.AppendDiagnostics("FontConverter ExtractFontsFromZipAsync", ex);
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