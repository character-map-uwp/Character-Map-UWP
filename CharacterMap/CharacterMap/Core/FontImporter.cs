using Windows.Storage.Search;
using WoffToOtf;

namespace CharacterMap.Core;

public static class FontImporter
{
    public const string PENDING = nameof(PENDING);
    public const string TEMP = nameof(TEMP);

    public static HashSet<string> SupportedFormats { get; } = new()
    {
        ".ttf", ".otf", ".otc", ".ttc", // ".woff", ".woff2"
    };

    public static HashSet<string> ImportFormats { get; } = new()
    {
        ".ttf", ".otf", ".otc", ".ttc", ".woff", ".zip", ".woff2",
        ".TTF", ".OTF", ".OTC", ".TTC", ".WOFF", ".ZIP", ".WOFF2"
    };

    public static StorageFolder ImportFolder { get; } = ApplicationData.Current.LocalFolder;

    /* If we can't delete a font during a session, we mark it here */
    public static HashSet<string> IgnoredFonts { get; } = new();





    //------------------------------------------------------
    //
    //  Import
    //
    //------------------------------------------------------

    internal static Task<FontImportResult> ImportFontsAsync(IReadOnlyList<IStorageItem> items)
    {
        return Task.Run(async () =>
        {
            List<StorageFile> imported = new();
            List<StorageFile> existing = new();
            List<(IStorageItem, string)> invalid = new();

            foreach (var item in items)
            {
                if (item is not StorageFile file)
                {
                    invalid.Add((item, Localization.Get("ImportNotAFile")));
                    continue;
                }

                if (!file.IsAvailable)
                {
                    invalid.Add((item, Localization.Get("ImportUnavailableFile")));
                    continue;
                }

                if (IgnoredFonts.Contains(file.Name))
                {
                    invalid.Add((item, Localization.Get("ImportPendingDelete")));
                    continue;
                }

                // For WOFF files we can attempt to convert the file to OTF before loading
                StorageFile src = file;
                var convertResult = await FontConverter.TryConvertAsync(file);
                if (convertResult.Result is not ConversionStatus.OK)
                {
                    invalid.Add((src, Localization.Get("ImportFailedWoff")));
                    continue;
                }
                else
                    file = convertResult.File;

                if (SupportedFormats.Contains(file.FileType.ToLower()))
                {
                    // TODO : What do we do if a font with the same file name already exists?

                    /* 
                     * Explicitly not using StorageFile/Folder API's here because there
                     * are some strange import bugs when checking a file exists 
                     */
                    if (!File.Exists(Path.Combine(ImportFolder.Path, file.Name)))
                    {
                        StorageFile fontFile;
                        try
                        {
                            /* 
                             * Copy to local folder. We can only verify font file when it's inside
                             * the App's Local folder due to permission restrictions 
                             */
                            fontFile = await file.CopyAsync(ImportFolder);
                        }
                        catch (Exception)
                        {
                            invalid.Add((file, Localization.Get("ImportFileCopyFail")));
                            continue;
                        }

                        try
                        {
                            if (DirectWrite.HasValidFonts(fontFile))
                                imported.Add(fontFile);
                            else
                                await HandleInvalidAsync();
                        }
                        catch (Exception)
                        {
                            await HandleInvalidAsync();
                        }

                        // https://github.com/character-map-uwp/Character-Map-UWP/issues/241
                        Task HandleInvalidAsync()
                        {
                            invalid.Add((file, Localization.Get("ImportUnsupportedFontType")));
                            return fontFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask();
                        }
                    }
                    else
                    {
                        existing.Add(file);
                    }
                }
                else
                    invalid.Add((file, Localization.Get("ImportUnsupportedFileType")));
            }


            // Reload font collection is we have had success
            if (imported.Count > 0)
                await FontFinder.LoadFontsAsync();

            return new FontImportResult(imported, existing, invalid);
        });
    }




    /// <summary>
    /// Returns true if all fonts were deleted.
    /// Returns false is some failed - these will be deleted on next start up.
    /// </summary>
    /// <param name="font"></param>
    /// <returns></returns>
    internal static async Task<bool> TryRemoveFontAsync(CMFontFamily font)
    {
        font.PrepareForDelete();

        var variants = font.Variants.Where(v => v.IsImported).ToList();
        var success = true;

        foreach (var variant in variants)
        {
            variant.Dispose();

            GC.Collect(); // required to prevent "File in use" exception

            try
            {
                if (await FontImporter.ImportFolder.TryGetItemAsync(variant.FileName)
                                    is StorageFile file)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch
            {
                FontImporter.IgnoredFonts.Add(variant.FileName);
                success = false;
            }
        }

        /* If we did get a "File In Use" or something similar, 
         * make sure we delete the file during the next start up */
        if (success == false)
        {
            var pending = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(PENDING, CreationCollisionOption.OpenIfExists);
            await FileIO.WriteLinesAsync(pending, IgnoredFonts);
        }

        await FontFinder.LoadFontsAsync();
        return success;
    }





    //------------------------------------------------------
    //
    //  Maintenance
    //
    //------------------------------------------------------

    internal static Task CleanUpTempFolderAsync()
    {
        return Task.Run(async () =>
        {
            var path = Path.Combine(ImportFolder.Path, TEMP);
            if (Directory.Exists(path))
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(path).AsTask().ConfigureAwait(false);
                foreach (var file in await folder.GetFilesAsync().AsTask().ConfigureAwait(false))
                {
                    try
                    {
                        await file.DeleteAsync().AsTask().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
            }
        });
    }

    public static Task CleanUpPendingDeletesAsync()
    {
        return Task.Run(async () =>
        {
            /* If we fail to delete a font at runtime, delete it now before we load anything */
            var path = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, FontImporter.PENDING);
            var fontPath = ApplicationData.Current.LocalFolder.Path;
            if (File.Exists(path) && await TryGetFileAsync(path).ConfigureAwait(false) is StorageFile file)
            {
                var lines = await FileIO.ReadLinesAsync(file).AsTask().ConfigureAwait(false);

                List<string> moreFails = new();
                foreach (var line in lines)
                {
                    if (await TryGetFileAsync(Path.Combine(fontPath, line)).ConfigureAwait(false) is StorageFile deleteFile)
                    {
                        try
                        {
                            await deleteFile.DeleteAsync().AsTask().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            moreFails.Add(line);
                        }
                    }
                }

                if (moreFails.Count > 0)
                    await FileIO.WriteLinesAsync(file, moreFails).AsTask().ConfigureAwait(false);
                else
                    await file.DeleteAsync().AsTask().ConfigureAwait(false);
            }
        });
    }

    private static async Task<StorageFile> TryGetFileAsync(string path)
    {
        try
        {
            return await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }





    //------------------------------------------------------
    //
    //  Font Loaders
    //
    //------------------------------------------------------

    internal static async Task<CMFontFamily> LoadFromFileAsync(StorageFile file)
    {
        await FontFinder.InitialiseAsync().ConfigureAwait(false);

        // 1. Convert Woff or Woff2 to OTF
        var convert = await FontConverter.TryConvertAsync(file);
        if (convert.Result != ConversionStatus.OK)
            return null;
        file = convert.File;

        // 2. Copy to temp storage
        var folder = await ImportFolder.CreateFolderAsync(TEMP, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
        var localFile = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

        // 3. Load fonts from file
        Dictionary<string, CMFontFamily> resultList = new();
        DWriteFontSet fontSet = Utils.GetInterop().GetFonts(localFile).Inflate();
        foreach (var font in fontSet.Fonts)
            FontFinder.AddFont(resultList, font, localFile);

        GC.Collect();
        return resultList.Count > 0 ? resultList.First().Value : null;
    }

    public static Task<FolderContents> LoadToTempFolderAsync(FolderOpenOptions options)
    {
        return Task.Run(async () =>
        {
            var folder = options.Root as StorageFolder;
            Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);

            List<StorageFile> files = new();

            try
            {
                var query = folder.CreateFileQueryWithOptions(
                    new QueryOptions(CommonFileQuery.DefaultQuery, ImportFormats)
                    {
                        FolderDepth = options.Recursive ? FolderDepth.Deep : FolderDepth.Shallow,
                        IndexerOption = IndexerOption.DoNotUseIndexer,

                    });

                files = (await query.GetFilesAsync().AsTask(options.Token.Value)).ToList();
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException) { }

            FolderContents contents = await LoadToTempFolderAsync(files, options).ConfigureAwait(false);
            if (options.IsCancelled)
                return contents;

            contents.UpdateFontSet();
            return contents;
        });
    }

    public static async Task<FolderContents> LoadZipToTempFolderAsync(StorageFile zipFile)
    {
        List<StorageFile> files = new() { zipFile };
        var contents = await LoadToTempFolderAsync(files, new FolderOpenOptions { AllowZip = true, Root = zipFile }).ConfigureAwait(false);
        contents.UpdateFontSet();
        return contents;
    }

    public static Task<FolderContents> LoadToTempFolderAsync(
        IReadOnlyList<StorageFile> files, FolderOpenOptions options, StorageFolder tempFolder = null, FolderContents contents = null)
    {
        return Task.Run(async () =>
        {
            await FontFinder.InitialiseAsync().ConfigureAwait(false);

            // 1. Create temporary storage folder
            var dest = tempFolder ?? await ApplicationData.Current.TemporaryFolder.CreateFolderAsync("i", CreationCollisionOption.GenerateUniqueName)
                .AsTask().ConfigureAwait(false);
            contents ??= new(options.Root, dest);
            Func<StorageFile, Task<List<StorageFile>>> func = (storageFile) => GetFontsAsync(storageFile, dest, options);
            if (options.IsCancelled)
                return contents;

            // 2. Copy all files to temporary storage
            List<Task<List<StorageFile>>> tasks = files
                .Where(f => ImportFormats.Contains(f.FileType.ToLower()))
                .Select(func)
                .ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (options.IsCancelled)
                return contents;

            // 3. Create font sets
            var interop = Utils.GetInterop();
            var results = tasks.Where(t => t.Result is not null).SelectMany(t => t.Result).ToList();
            var dwSets = interop.GetFonts(results).ToList();

            // 4. Create InstalledFonts list
            for (int i = 0; i < dwSets.Count; i++)
            {
                StorageFile file = results[i];
                DWriteFontSet set = dwSets[i];

                foreach (DWriteFontFace font in set.Inflate().Fonts)
                    FontFinder.AddFont(contents.FontCache, font, file);
            }

            return contents;
        });

    }

    private static async Task<List<StorageFile>> GetFontsAsync(StorageFile f, StorageFolder dest, FolderOpenOptions options)
    {
        if (options.IsCancelled)
            return new();

        List<StorageFile> results = new();

        if (f.FileType == ".zip")
        {
            if (options.AllowZip)
                results = await FontConverter.ExtractFontsFromZipAsync(f, dest, options).ConfigureAwait(false);
        }
        else
        {
            // Attempt to convert .woff & .woff2 to OTF
            var convert = await FontConverter.TryConvertAsync(f, dest).ConfigureAwait(false);
            if (convert.Result is not ConversionStatus.OK || options.IsCancelled)
                goto Exit;

            if (convert.File == f)
            {
                var file = await f.CopyAsync(dest, f.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
                results.Add(file);
            }
            else
                results.Add(convert.File);
        }

    Exit:
        options?.Increment(results.Count);
        return results;
    }

}
