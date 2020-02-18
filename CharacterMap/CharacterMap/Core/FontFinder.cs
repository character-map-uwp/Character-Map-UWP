using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CharacterMap.Helpers;
using CharacterMapCX;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Graphics.Canvas.Text;
using Windows.Storage;
using Windows.UI.Text;

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
        const string PENDING = nameof(PENDING);
        const string TEMP = nameof(TEMP);

        private static SemaphoreSlim _initSemaphore { get; } = new SemaphoreSlim(1,1);
        private static SemaphoreSlim _loadSemaphore { get; } = new SemaphoreSlim(1,1);

        /* If we can't delete a font during a session, we mark it here */
        private static HashSet<string> _ignoredFonts { get; } = new HashSet<string>();

        private static StorageFolder _importFolder => ApplicationData.Current.LocalFolder;

        public static List<InstalledFont> Fonts { get; private set; }

        public static InstalledFont DefaultFont { get; private set; }

        public static bool HasAppxFonts                { get; private set; }
                
        public static bool HasRemoteFonts              { get; private set; }

        public static HashSet<string> SupportedFormats { get; } = new HashSet<string>
        {
            ".ttf", ".otf", ".otc", ".ttc", // ".woff", ".woff2"
        };

        public static async Task<DWriteFontSet> InitialiseAsync()
        {
            await _initSemaphore.WaitAsync().ConfigureAwait(false);

            Interop interop = SimpleIoc.Default.GetInstance<Interop>();
            DWriteFontSet systemFonts = interop.GetSystemFonts();

            try
            {
                if (DefaultFont == null)
                {
                    await CleanUpTempFolderAsync().ConfigureAwait(false);
                    await CleanUpPendingDeletesAsync().ConfigureAwait(false);

                    var segoe = systemFonts.Fonts.FirstOrDefault(f =>
                    {
                        return f.FontFace.FamilyNames.Values.Contains("Segoe UI")
                                && f.FontFace.Weight.Weight == FontWeights.Normal.Weight
                                && f.FontFace.Stretch == FontStretch.Normal
                                && f.FontFace.Style == FontStyle.Normal;
                    });

                    if (segoe != null)
                    {
                        DefaultFont = new InstalledFont
                        {
                            Name = "",
                            FontFace = segoe.FontFace,
                            Variants = new List<FontVariant> { FontVariant.CreateDefault(segoe.FontFace) }
                        };
                    }
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
                DWriteFontSet systemFonts = await InitialiseAsync().ConfigureAwait(false);
                UpdateMeta(systemFonts);

                await _loadSemaphore.WaitAsync().ConfigureAwait(false);

                Interop interop = SimpleIoc.Default.GetInstance<Interop>();
                Dictionary<string, InstalledFont> resultList = new Dictionary<string, InstalledFont>(systemFonts.Fonts.Count);

                /* Add all system fonts */
                foreach(DWriteFontFace font in systemFonts.Fonts)
                {
                    AddFont(resultList, font, interop);
                }

                /* Add imported fonts */
                var files = await _importFolder.GetFilesAsync().AsTask().ConfigureAwait(false);
                foreach (var file in files.OfType<StorageFile>())
                {
                    if (_ignoredFonts.Contains(file.Name))
                        continue;

                    DWriteFontSet importedFonts = interop.GetFonts(new Uri(GetAppPath(file)));
                    UpdateMeta(importedFonts);
                    foreach (DWriteFontFace font in importedFonts.Fonts)
                    {
                        AddFont(resultList, font, interop, file);
                    }
                }

                /* Order everything appropriately */
                Fonts = resultList.OrderBy(f => f.Key).Select(f =>
                {
                    f.Value.Variants = f.Value.Variants.OrderBy(v => v.FontFace.Weight.Weight).ToList();
                    return f.Value;
                }).ToList();

                _loadSemaphore.Release();
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateMeta(DWriteFontSet set)
        {
            HasRemoteFonts = HasRemoteFonts || set.CloudFontCount > 0;
            HasAppxFonts = HasAppxFonts || set.AppxFontCount > 0;
        }

        /* 
         * Helper method for adding fonts. 
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddFont(
            IDictionary<string, InstalledFont> fontList,
            DWriteFontFace font,
            Interop interop = null,
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
                        var variant = new FontVariant(font.FontFace, file, font.Properties);
                        if (file != null)
                            fontFamily.HasImportedFiles = true;

                        fontFamily.Variants.Add(variant);
                    }
                    else
                    {
                        fontList[familyName] = new InstalledFont
                        {
                            Name = familyName,
                            IsSymbolFont = font.FontFace.IsSymbolFont,
                            FontFace = font.FontFace,
                            Variants = new List<FontVariant> { new FontVariant(font.FontFace, file, font.Properties) },
                            HasImportedFiles = file != null
                        };
                    }
                }
                else
                {

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

                Interop interop = SimpleIoc.Default.GetInstance<Interop>();

                foreach (var item in items)
                {
                    if (!(item is StorageFile file))
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

                    if (SupportedFormats.Contains(file.FileType.ToLower()))
                    {
                        // TODO : What do we do if a font with the same file name already exists?

                        /* Explicilty not using StorageFile/Folder API's here because there
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
                            if (interop.HasValidFonts(new Uri(GetAppPath(fontFile))))
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
            font.FontFace = null;
            var variants = font.Variants.Where(v => v.IsImported).ToList();

            var success = true;

            foreach (var variant in variants)
            {
                font.Variants.Remove(variant);
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
                if (File.Exists(path) && await TryGetFileAsync(path).ConfigureAwait(false) is StorageFile file)
                {
                    var lines = await FileIO.ReadLinesAsync(file).AsTask().ConfigureAwait(false);

                    var moreFails = new List<string>();
                    foreach (var line in lines)
                    {
                        if (await TryGetFileAsync(line).ConfigureAwait(false) is StorageFile deleteFile)
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

            var folder = await _importFolder.CreateFolderAsync(TEMP, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
            var localFile = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

            var resultList = new Dictionary<string, InstalledFont>();

            Interop interop = SimpleIoc.Default.GetInstance<Interop>();
            DWriteFontSet fontSet = interop.GetFonts(new Uri(GetAppPath(localFile)));
            foreach (DWriteFontFace font in fontSet.Fonts)
            {
                AddFont(resultList, font, interop, localFile);
            }

            GC.Collect();
            return resultList.Count > 0 ? resultList.First().Value : null;
        }

        public static bool IsMDL2(FontVariant variant) => variant.FamilyName.Contains("MDL2 Assets");

    }
}