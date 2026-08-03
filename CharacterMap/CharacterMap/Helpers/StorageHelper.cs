using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Documents;

namespace CharacterMap.Helpers;

internal static class StorageHelper
{
    private static string CurrentTempFolderName = "T";
    public static string CurrentTempPath => Path.Combine(ApplicationData.Current.TemporaryFolder.Path, CurrentTempFolderName);

    public static string AppStorageFolder => field ?? Path.GetDirectoryName(ApplicationData.Current.TemporaryFolder.Path);

    /// <summary>
    /// XAML Glyphs control can only read from local URI's, so we copy fonts in here
    /// </summary>
    private static StorageFolder _glyphsFolder = null;

    /// <summary>
    /// Make sure we're  maintaining the size of our temp folder by clearing it at the start of every run.
    /// (Windows can also delete the contents of this folder if it needs to)
    /// </summary>
    /// <returns></returns>
    public static Task PrepareTempAsync()
    {
        return Task.Run(async () =>
        {
            // 1. Find existing temp dirs
            var dirs = Directory.EnumerateDirectories(ApplicationData.Current.TemporaryFolder.Path).ToList();

            // 2. Set new temp dir for this session
            do
            {
                CurrentTempFolderName = Utils.Random.Next(0, 1000).ToString();
            }
            while (dirs.Any(d => Path.GetDirectoryName(d) == CurrentTempFolderName));

            // 3. Precreate folders for this session
            _glyphsFolder = await CreateTempFolderAsync("GFC").AsTask().ConfigureAwait(false); // Glyph file cache

            // 4. Delete existing temp files
            foreach (var dir in dirs)
                Directory.Delete(dir, true);

        });
    }

    public static void TryDeleteTemp()
    {
        // Try to proactively delete the temp storage folder to respect their disk space.
        // This might fail if files are in use - the folder will be cleaned up on next launch anyway
        try
        {
            Directory.Delete(CurrentTempPath, true);
        }
        catch
        {
            Debugger.Break();
        }
    }

    public static IAsyncOperation<StorageFolder> CreateTempFolderAsync(string path, CreationCollisionOption option = CreationCollisionOption.GenerateUniqueName)
    {
        // TODO : T can change per session
        return ApplicationData.Current.TemporaryFolder.CreateFolderAsync(Path.Combine(CurrentTempFolderName, path), option);
    }

    public static IAsyncOperation<StorageFile> CreateTempFileAsync(string name, CreationCollisionOption option = CreationCollisionOption.GenerateUniqueName)
    {
        // TODO : T can change per session
        return ApplicationData.Current.TemporaryFolder.CreateFileAsync(Path.Combine(CurrentTempFolderName, name), option);
    }

    public static IAsyncOperation<StorageFolder> PickFolderAsync()
    {
        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        return picker.PickSingleFolderAsync();
    }

    public static IAsyncOperation<StorageFile> PickOpenFileAsync(IEnumerable<string> fileTypes, string commitText)
    {
        FileOpenPicker picker = new ();
        foreach (var format in fileTypes)
            picker.FileTypeFilter.Add(format);

        picker.CommitButtonText = commitText;
        return picker.PickSingleFileAsync();
    }

    public static async Task<StorageFile> PickSaveFileAsync(
        string fileName,
        string key,
        IList<string> values,
        PickerLocationId suggestedLocation = PickerLocationId.PicturesLibrary)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedFileName = fileName
        };

        if (suggestedLocation != PickerLocationId.Unspecified)
            savePicker.SuggestedStartLocation = suggestedLocation;

        savePicker.FileTypeChoices.Add(key, values);

        try
        {
            return await savePicker.PickSaveFileAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task DeleteAsync(this StorageFolder folder, bool deleteFolder = false)
    {
        // 0. We have guaranteed access to use the System.IO api's within our own application
        //    storage folders
        if (deleteFolder && folder.Path.StartsWith(AppStorageFolder))
        {
            await Task.Run(() => Directory.Delete(folder.Path, true));
            return;
        }

        // 1. Delete all child folders
        var folders = await folder.GetFoldersAsync().AsTask().ConfigureAwait(false);
        if (folders.Count > 0)
        {
            var tasks = folders.Select(f => DeleteAsync(f, true));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        // 2. Delete child files
        var files = await folder.GetFilesAsync().AsTask().ConfigureAwait(false);
        if (files.Count > 0)
        {
            var tasks = files.Select(f => f.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask());
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        // 3. Delete folder
        if (deleteFolder)
            await folder.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
    }

    private static Dictionary<string, Uri> _glyphFileCache = new();

    public static async Task<Uri> GetTempGlyphsLocalCopyAsync(CMFontFace fontFace)
    {
        var path = DirectWrite.GetFileName(fontFace.Face);
        var name = Path.GetFileName(path);

        // Cloud provider fonts will head down this path
        if (string.IsNullOrWhiteSpace(path))
        {
            name = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
        }

        var key = name + fontFace.Source;

        if (!_glyphFileCache.TryGetValue(key, out Uri uri))
        {
            // Cache to a folder XAML glyphs control can read.
            // It's possible the UI might let us into this method multiple times for the same font, but we don't really care;
            // we'll use GenerateUniqueName to avoid throwing, just in case.
            var file = await _glyphsFolder.CreateFileAsync(name, CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
            using (var s = await file.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false))
            {
                await DirectWrite.WriteToStreamAsync(fontFace.Face, s).AsTask().ConfigureAwait(false);
            }

            // If this was cached by another caller already, delete ourself
            if (_glyphFileCache.TryGetValue(key, out uri))
                _ = file.TryDeleteAsync().ConfigureAwait(false);
            else
                _glyphFileCache[key] = uri = new Uri(file.GetAppPath(), UriKind.Absolute);
        }

        return uri;
    }

    public static async Task<bool> TryDeleteAsync(this StorageFile file)
    {
        try
        {
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts a full file system path into an ms-appdata:/// path if appropriate
    /// </summary>
    public static string GetAppPath(this StorageFile file) => GetAppPath(file.Path);

    /// <summary>
    /// Converts a full file system path into an ms-appdata:/// path if appropriate
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetAppPath(string path)
    {
        if (path.StartsWith(ApplicationData.Current.TemporaryFolder.Path, StringComparison.InvariantCultureIgnoreCase))
        {
            var str = path.Replace(ApplicationData.Current.TemporaryFolder.Path, "ms-appdata:///temp", StringComparison.InvariantCultureIgnoreCase)
                .Replace("\\", "/");
            return str;
        }
        var temp = Path.GetDirectoryName(path).EndsWith(FontImporter.TEMP);
        return $"ms-appdata:///local/{(temp ? $"{FontImporter.TEMP}/" : string.Empty)}{Path.GetFileName(path)}";
    }
}
