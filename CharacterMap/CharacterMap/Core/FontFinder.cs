using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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

        private static StorageFolder ImportFolder => ApplicationData.Current.LocalFolder;

        public static List<InstalledFont> Fonts { get; private set; }

        public static InstalledFont DefaultFont { get; private set; }

        public static HashSet<string> SupportedFormats { get; } = new HashSet<string>
        {
            ".ttf", ".otf", ".otc", ".ttc", // ".woff", ".woff2"
        };

        public static async Task<CanvasFontSet> InitialiseAsync()
        {
            await _initSemaphore.WaitAsync().ConfigureAwait(false);

            Interop interop = SimpleIoc.Default.GetInstance<Interop>();
            CanvasFontSet systemFonts = interop.GetSystemFonts(true);

            try
            {
                if (DefaultFont == null)
                {
                    await CleanUpTempFolderAsync().ConfigureAwait(false);
                    await CleanUpPendingDeletesAsync().ConfigureAwait(false);

                    var segoe = systemFonts.GetMatchingFonts("Segoe UI", FontWeights.Normal, FontStretch.Normal, FontStyle.Normal);
                    if (segoe != null && segoe.Fonts.Count > 0)
                    {
                        DefaultFont = new InstalledFont
                        {
                            Name = "",
                            FontFace = segoe.Fonts[0],
                            Variants = new List<FontVariant> { FontVariant.CreateDefault(segoe.Fonts[0]) }
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

                var systemFonts = await InitialiseAsync();

                await _loadSemaphore.WaitAsync();

                var familyCount = systemFonts.Fonts.Count;
                var resultList = new Dictionary<string, InstalledFont>();

                /* Add all system fonts */
                for (var i = 0; i < familyCount; i++)
                {
                    AddFont(resultList, systemFonts.Fonts[i]);
                }

                /* Add imported fonts */
                var files = await ImportFolder.GetFilesAsync();
                foreach (var file in files.OfType<StorageFile>())
                {
                    if (_ignoredFonts.Contains(file.Name))
                        continue;

                    foreach (var font in GetFontFacesFromFile(file))
                        AddFont(resultList, font, file);
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

        /* 
         * Helper method for adding fonts. 
         */
        private static void AddFont(IDictionary<string, InstalledFont> fontList, CanvasFontFace fontFace, StorageFile file = null)
        {
            try
            {
                var familyNames = fontFace.FamilyNames;
                if (!familyNames.TryGetValue(CultureInfo.CurrentCulture.Name, out var familyName))
                {
                    if (!familyNames.TryGetValue("en-us", out familyName))
                    {
                        if (familyNames != null && familyName.Length > 0)
                            familyName = familyNames.FirstOrDefault().Value;
                    }
                }

                if (!string.IsNullOrEmpty(familyName))
                {
                    /* Check if we already have a listing for this fontFamily */
                    if (fontList.TryGetValue(familyName, out var fontFamily))
                    {
                        var variant = new FontVariant(fontFace, familyName, file);
                        if (file != null)
                            fontFamily.HasImportedFiles = true;

                        fontFamily.Variants.Add(variant);
                    }
                    else
                    {
                        fontList[familyName] = new InstalledFont
                        {
                            Name = familyName,
                            IsSymbolFont = fontFace.IsSymbolFont,
                            FontFace = fontFace,
                            Variants = new List<FontVariant> { new FontVariant(fontFace, familyName, file) },
                            HasImportedFiles = file != null
                        };
                    }
                }
            }
            catch (Exception)
            {
                // Corrupted font files throw an exception
            }
        }

        private static IEnumerable<CanvasFontFace> GetFontFacesFromFile(StorageFile file)
        {
            using (var set = new CanvasFontSet(new Uri(GetAppPath(file))))
            {
                return set.Fonts.ToList();
            }
        }

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

                var interop = SimpleIoc.Default.GetInstance<Interop>();

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
                        if (!File.Exists(Path.Combine(ImportFolder.Path, file.Name)))
                        {
                            StorageFile fontFile;
                            try
                            {
                                /* Copy to local folder. We can only verify font file when it's inside
                                * the App's Local folder due to CanvasFontSet file restrictions */
                                fontFile = await file.CopyAsync(ImportFolder);
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
                    if (await ImportFolder.TryGetItemAsync(variant.FileName)
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


        private static Task CleanUpPendingDeletesAsync()
        {
            return Task.Run(() =>
            {
                /* If we fail to delete a font at runtime, delete it now before we load anything */
                var path = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, PENDING);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);

                    var moreFails = new List<string>();
                    foreach (var line in lines)
                    {
                        if (File.Exists(line))
                        {
                            try
                            {
                                File.Delete(line);
                            }
                            catch (Exception)
                            {
                                moreFails.Add(line);
                            }
                        }
                    }

                    if (moreFails.Count > 0)
                        File.WriteAllLines(path, moreFails);
                    else
                        File.Delete(path);
                }
            });
            
        }

        private static Task CleanUpTempFolderAsync()
        {
            return Task.Run(() =>
            {
                var path = Path.Combine(ImportFolder.Path, TEMP);
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path))
                    {
                        try
                        {
                            File.Delete(file);
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

            var folder = await ImportFolder.CreateFolderAsync(TEMP, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
            var localFile = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

            var resultList = new Dictionary<string, InstalledFont>();
            foreach (var font in GetFontFacesFromFile(localFile))
                AddFont(resultList, font, localFile);

            GC.Collect();

            return resultList.Count > 0 ? resultList.First().Value : null;
        }

        public static bool IsMDL2(FontVariant variant) => variant.FamilyName.Contains("MDL2");

    }
}