using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMapCX;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Windows.Storage;
using Windows.UI.Text;
using WoffToOtf;

namespace CharacterMap.Core
{
    public class FontImportResult
    {
        public FontImportResult(List<StorageFile> imported, List<StorageFile> existing, List<(IStorageItem, string)> invalid)
        {
            Imported = imported;
            Existing = existing;
            Invalid = invalid;
        }

        public List<StorageFile> Imported { get; }
        public List<StorageFile> Existing { get; }
        public List<(IStorageItem, string)> Invalid { get; }
    }

    public class FontFinder
    {
        private const string PENDING = nameof(PENDING);
        private const string TEMP = nameof(TEMP);

        private static SemaphoreSlim _initSemaphore { get; } = new SemaphoreSlim(1,1);
        private static SemaphoreSlim _loadSemaphore { get; } = new SemaphoreSlim(1,1);

        /* If we can't delete a font during a session, we mark it here */
        private static HashSet<string> _ignoredFonts { get; } = new HashSet<string>();

        private static StorageFolder _importFolder => ApplicationData.Current.LocalFolder;

        public static IReadOnlyList<InstalledFont> Fonts         { get; private set; }
        public static IReadOnlyList<InstalledFont> ImportedFonts { get; private set; }

        public static InstalledFont DefaultFont { get; private set; }

        public static bool HasAppxFonts                { get; private set; }
                
        public static bool HasRemoteFonts              { get; private set; }

        public static bool HasVariableFonts            { get; private set; }

        public static DWriteFallbackFont Fallback       { get; private set; }

        public static HashSet<string> SupportedFormats { get; } = new HashSet<string>
        {
            ".ttf", ".otf", ".otc", ".ttc", // ".woff", ".woff2"
        };

        public static HashSet<string> ImportFormats { get; } = new HashSet<string>
        {
            ".ttf", ".otf", ".otc", ".ttc", ".woff"//, ".woff2"
        };

        public static async Task<DWriteFontSet> InitialiseAsync()
        {
            await _initSemaphore.WaitAsync().ConfigureAwait(false);

            var interop = Ioc.Default.GetService<NativeInterop>();
            var systemFonts = interop.GetSystemFonts();

            try
            {
                if (DefaultFont == null)
                {
                    await Task.WhenAll(
                        CleanUpTempFolderAsync(),
                        CleanUpPendingDeletesAsync()).ConfigureAwait(false);

                    var segoe = systemFonts.Fonts.FirstOrDefault(
                           f => f.FontFace.FamilyNames.Values.Contains("Segoe UI")
                        && f.FontFace.Weight.Weight == FontWeights.Normal.Weight
                        && f.FontFace.Stretch == FontStretch.Normal
                        && f.FontFace.Style == FontStyle.Normal);

                    if (segoe != null)
                        DefaultFont = InstalledFont.CreateDefault(segoe);
                }
            }
            finally
            {
                _initSemaphore.Release();
            }

            return systemFonts;
        }
        public static Task LoadFontsAsync()
        {
            Fonts = null;

            return Task.Run(async () =>
            {
                // Reset meta to false;
                UpdateMeta(null);

                await _loadSemaphore.WaitAsync().ConfigureAwait(false);
                var interop = Ioc.Default.GetService<NativeInterop>();

                // Load SystemFonts and imported fonts in parallel
                IReadOnlyList<StorageFile> files = null;
                Task<List<DWriteFontSet>> setsTask = Task.Run(async () =>
                {
                    files = await _importFolder.GetFilesAsync();
                    return interop.GetFonts(files).ToList();
                });
                Task<DWriteFontSet> init = InitialiseAsync();
                await Task.WhenAll(init, setsTask);

                // Load in System Fonts
                var systemFonts = init.Result;
                Dictionary<string, InstalledFont> resultList = new (systemFonts.Fonts.Count);
                UpdateMeta(systemFonts);

                /* Add imported fonts */
                IReadOnlyList<DWriteFontSet> sets = setsTask.Result;
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (_ignoredFonts.Contains(file.Name))
                        continue;

                    DWriteFontSet importedFonts = sets[i];
                    UpdateMeta(importedFonts);
                    foreach (DWriteFontFace font in importedFonts.Fonts)
                    {
                        AddFont(resultList, font, file);
                    }
                }

                var imports = resultList.ToDictionary(d => d.Key, v => v.Value.Clone());

                /* Add all system fonts */
                foreach (var font in systemFonts.Fonts)
                    AddFont(resultList, font);
                

                /* Order everything appropriately */
                Fonts = CreateFontList(resultList);
                ImportedFonts = CreateFontList(imports);


                if (Fallback == null)
                    Fallback = interop.CreateEmptyFallback();

                _loadSemaphore.Release();

                WeakReferenceMessenger.Default.Send(new FontListCreatedMessage());
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dictionary<string, InstalledFont> CreateFontCollection(NativeInterop interop, IReadOnlyList<DWriteFontFace> fonts)
        {
            var resultList = new Dictionary<string, InstalledFont>(fonts.Count);

            /* Add all system fonts */
            foreach (var font in fonts)
            {
                AddFont(resultList, font);
            }

            return resultList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<InstalledFont> CreateFontList(Dictionary<string, InstalledFont> fonts)
        {
            return fonts.OrderBy(f => f.Key).Select(f =>
            {
                f.Value.SortVariants();
                return f.Value;
            }).ToList();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateMeta(DWriteFontSet set)
        {
            if (set == null)
            {
                HasRemoteFonts = HasAppxFonts = HasVariableFonts = false;
                return;
            }

            HasRemoteFonts = HasRemoteFonts || set.CloudFontCount > 0;
            HasAppxFonts = HasAppxFonts || set.AppxFontCount > 0;
            HasVariableFonts = HasVariableFonts || set.VariableFontCount > 0;
        }

        internal static List<FontVariant> GetImportedVariants()
        {
            return Fonts.Where(f => f.HasImportedFiles)
                        .SelectMany(f => f.Variants.Where(v => v.IsImported))
                        .ToList();
        }

        /* 
         * Helper method for adding fonts. 
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddFont(
            IDictionary<string, InstalledFont> fontList,
            DWriteFontFace font,
            StorageFile file = null)
        {
            try
            {
                var familyName = font.Properties.FamilyName;
                if (!string.IsNullOrEmpty(familyName))
                {
                    /* Check if we already have a listing for this fontFamily */
                    if (fontList.TryGetValue(familyName, out var fontFamily))
                    {
                        fontFamily.AddVariant(font, file);
                    }
                    else
                    {
                        fontList[familyName] = new InstalledFont(familyName, font, file);
                    }
                }
            }
            catch (Exception)
            {
                // Corrupted font files throw an exception
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string GetAppPath(StorageFile file)
        {
            var temp = Path.GetDirectoryName(file.Path).EndsWith(TEMP);
            return $"ms-appdata:///local/{(temp ? $"{TEMP}/" :  string.Empty)}{file.Name}";
        }

        internal static Task<FontImportResult> ImportFontsAsync(IReadOnlyList<IStorageItem> items)
        {
            return Task.Run(async () =>
            {
                var imported = new List<StorageFile>();
                var existing = new List<StorageFile>();
                var invalid = new List<(IStorageItem, string)>();

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

                    if (_ignoredFonts.Contains(file.Name))
                    {
                        invalid.Add((item, Localization.Get("ImportPendingDelete")));
                        continue;
                    }

                    // For WOFF files we can attempt to convert the file to OTF before loading
                    var src = file;
                    var convertResult = await FontConverter.TryConvertAsync(file);
                    if (convertResult.Result is not ConversionStatus.OK)
                    {
                        if (convertResult.Result == ConversionStatus.UnsupportedWOFF2)
                            invalid.Add((src, Localization.Get("ImportWOFF2NotSupported")));
                        else
                            invalid.Add((src, Localization.Get("ImportFailedWoff")));
                        continue;
                    }
                    else
                        file = convertResult.File;

                    if (SupportedFormats.Contains(file.FileType.ToLower()))
                    {
                        // TODO : What do we do if a font with the same file name already exists?

                        /* Explicitly not using StorageFile/Folder API's here because there
                         * are some strange import bugs when checking a file exists */
                        if (!File.Exists(Path.Combine(_importFolder.Path, file.Name)))
                        {
                            StorageFile fontFile;
                            try
                            {
                                /* Copy to local folder. We can only verify font file when it's inside
                                * the App's Local folder due to CanvasFontSet file restrictions */
                                fontFile = await file.CopyAsync(_importFolder);
                            }
                            catch (Exception)
                            {
                                invalid.Add((file, Localization.Get("ImportFileCopyFail")));
                                continue;
                            }
                           

                            /* Avoid Garbage Collection (?) issue preventing immediate file deletion 
                             * by dropping to C++ */
                            if (DirectWrite.HasValidFonts(new Uri(GetAppPath(fontFile))))
                            {
                                imported.Add(fontFile);
                            }
                            else
                            {
                                invalid.Add((file, Localization.Get("ImportUnsupportedFontType")));
                                await fontFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
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

                if (imported.Count > 0)
                {
                    await LoadFontsAsync();
                }

                return new FontImportResult(imported, existing, invalid);
            });
        }

        

        /// <summary>
        /// Returns true if all fonts were deleted.
        /// Returns false is some failed - these will be deleted on next start up.
        /// </summary>
        /// <param name="font"></param>
        /// <returns></returns>
        internal static async Task<bool> RemoveFontAsync(InstalledFont font)
        {
            Fonts = null;
            font.PrepareForDelete();

            var variants = font.Variants.Where(v => v.IsImported).ToList();
            var success = true;

            foreach (var variant in variants)
            {
                variant.Dispose();

                GC.Collect(); // required to prevent "File in use" exception

                try
                {
                    if (await _importFolder.TryGetItemAsync(variant.FileName)
                                        is StorageFile file)
                    {
                        await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
                catch 
                {
                    _ignoredFonts.Add(variant.FileName);
                    success = false;
                }
            }

            /* If we did get a "File In Use" or something similar, 
             * make sure we delete the file during the next start up */
            if (success == false)
            {
                var pending = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(PENDING, CreationCollisionOption.OpenIfExists);
                await FileIO.WriteLinesAsync(pending, _ignoredFonts);
            }

            await LoadFontsAsync();
            return success;
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

        private static Task CleanUpPendingDeletesAsync()
        {
            return Task.Run(async() =>
            {
                /* If we fail to delete a font at runtime, delete it now before we load anything */
                var path = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, PENDING);
                var fontPath = ApplicationData.Current.LocalFolder.Path;
                if (File.Exists(path) && await TryGetFileAsync(path).ConfigureAwait(false) is StorageFile file)
                {
                    var lines = await FileIO.ReadLinesAsync(file).AsTask().ConfigureAwait(false);

                    var moreFails = new List<string>();
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

        private static Task CleanUpTempFolderAsync()
        {
            return Task.Run(async () =>
            {
                var path = Path.Combine(_importFolder.Path, TEMP);
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

        internal static async Task<InstalledFont> LoadFromFileAsync(StorageFile file)
        {
            await InitialiseAsync().ConfigureAwait(false);

            var convert = await FontConverter.TryConvertAsync(file);
            if (convert.Result != ConversionStatus.OK)
                return null;

            file = convert.File;

            var folder = await _importFolder.CreateFolderAsync(TEMP, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
            var localFile = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

            var resultList = new Dictionary<string, InstalledFont>();

            var interop = Ioc.Default.GetService<NativeInterop>();
            var fontSet = interop.GetFonts(localFile);

            //var fontSet = DirectWrite.GetFonts(new Uri(GetAppPath(localFile)));
            foreach (var font in fontSet.Fonts)
            {
                AddFont(resultList, font, localFile);
            }

            GC.Collect();
            return resultList.Count > 0 ? resultList.First().Value : null;
        }

        public static bool IsMDL2(FontVariant variant) => variant != null && (variant.FamilyName.Contains("MDL2") || variant.FamilyName.Equals("Segoe Fluent Icons"));
        public static bool IsSystemSymbolFamily(FontVariant variant) => variant != null && (
            variant.FamilyName.Equals("Segoe MDL2 Assets") || variant.FamilyName.Equals("Segoe Fluent Icons"));

    }
}