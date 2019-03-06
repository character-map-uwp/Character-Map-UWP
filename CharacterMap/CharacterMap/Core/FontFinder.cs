using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        /* If we can't delete a font during a session, we mark it here */
        private static HashSet<string> _ignoredFonts { get; } = new HashSet<string>();

        private static StorageFolder ImportFolder => ApplicationData.Current.LocalFolder;

        public static List<InstalledFont> Fonts { get; private set; }

        public static InstalledFont DefaultFont { get; private set; }


        public static Task LoadFontsAsync()
        {
            Fonts = null;

            return Task.Run(async () =>
            {
                if (DefaultFont == null)
                {
                    /* Don't do this in the same IF below. We need to do it before loading FontSets */
                    await CleanUpPendingDeletesAsync();
                }

                var systemFonts = CanvasFontSet.GetSystemFontSet();

                if (DefaultFont == null)
                {
                    /* Set default font */
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


                var familyCount = systemFonts.Fonts.Count;
                Dictionary<string, InstalledFont> resultList = new Dictionary<string, InstalledFont>();


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
            });
        }

        /* 
                 * Helper method for adding fonts. 
                 */
        private static void AddFont(Dictionary<string, InstalledFont> fontList, CanvasFontFace fontFace, StorageFile file = null)
        {
            try
            {
                var familyNames = fontFace.FamilyNames;
                if (!familyNames.TryGetValue(CultureInfo.CurrentCulture.Name, out string familyName))
                {
                    familyNames.TryGetValue("en-us", out familyName);
                }

                if (familyName != null)
                {
                    /* Check if we already have a listing for this fontFamily */
                    if (fontList.TryGetValue(familyName, out InstalledFont fontFamily))
                    {
                        var variant = new FontVariant(fontFace, familyName, file);
                        if (file != null)
                            fontFamily.HasImportedFiles = true;

                        fontFamily.Variants.Add(variant);
                    }
                    else
                    {
                        var family = new InstalledFont
                        {
                            Name = familyName,
                            IsSymbolFont = fontFace.IsSymbolFont,
                            FontFace = fontFace,
                            Variants = new List<FontVariant> { new FontVariant(fontFace, familyName, file) }
                        };

                        if (file != null)
                        {
                            family.HasImportedFiles = true;
                        }

                        fontList[familyName] = family;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupted font files throw an exception
            }
        }

        private static List<CanvasFontFace> GetFontFacesFromFile(StorageFile file)
        {
            using (CanvasFontSet set = new CanvasFontSet(new Uri(GetAppPath(file))))
            {
                return set.Fonts.ToList();
            }
        }

        internal static string GetAppPath(StorageFile file)
        {
            bool temp = Path.GetDirectoryName(file.Path).EndsWith(TEMP);
            return $"ms-appdata:///local/{(temp ? $"{TEMP}/" :  string.Empty)}{file.Name}";
        }

        internal static Task<FontImportResult> ImportFontsAsync(IReadOnlyList<IStorageItem> items)
        {
            return Task.Run(async () =>
            {
                List<StorageFile> _imported = new List<StorageFile>();
                List<StorageFile> _existing = new List<StorageFile>();
                List<(IStorageItem, string)> _invalid = new List<(IStorageItem, string)>();

                Interop interop = SimpleIoc.Default.GetInstance<Interop>();

                foreach (var item in items)
                {
                    if (!(item is StorageFile file))
                    {
                        _invalid.Add((item, "Not a file"));
                        continue;
                    }

                    if (!file.IsAvailable)
                    {
                        _invalid.Add((item, "File not available"));
                        continue;
                    }

                    if (_ignoredFonts.Contains(file.Name))
                    {
                        _invalid.Add((item, "Existing font pending delete. Please restart app."));
                        continue;
                    }

                    if (file.FileType.ToUpper().EndsWith("TTF")
                        || file.FileType.ToUpper().EndsWith("OTF"))
                    {
                        // TODO : What do we do if a font with the same file name already exists?

                        /* Explicilty not using StorageFile/Folder API's here because there
                         * are some strange import bugs when checking a file exists */
                        if (!File.Exists(Path.Combine(ImportFolder.Path, file.Name)))
                        {
                            /* Copy to local folder. We can only verify font file when it's inside
                             * the App's Local folder due to CanvasFontSet file restrictions */
                            StorageFile fontFile = await file.CopyAsync(ApplicationData.Current.LocalFolder);

                            /* Avoid Garbage Collection (?) issue preventing immediate file deete 
                             * by dropping to C++ */
                            if (interop.HasValidFonts(new Uri(GetAppPath(fontFile))))
                            {
                                _imported.Add(fontFile);
                            }
                            else
                            {
                                _invalid.Add((file, "Unsupported Font Type"));
                                await fontFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                        }
                        else
                        {
                            _existing.Add(file);
                        }
                    }
                    else
                        _invalid.Add((file, "Unsupported File Type"));
                }

                if (_imported.Count > 0)
                {
                    await LoadFontsAsync();
                }

                return new FontImportResult(_imported, _existing, _invalid);
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

            bool success = true;

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


        private static async Task CleanUpPendingDeletesAsync()
        {
            /* If we fail to delete a font at runtime, delete it now before we load anything */
            if (await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(PENDING)
                is StorageFile pendingFile)
            {
                var lines = (await FileIO.ReadLinesAsync(pendingFile)).ToList();

                List<string> moreFails = new List<string>();
                foreach (var line in lines)
                {
                    if (await ImportFolder.TryGetItemAsync(line) is StorageFile file)
                    {
                        try
                        {
                            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                        catch (Exception)
                        {
                            moreFails.Add(file.Name);
                        }
                    }
                }

                if (moreFails.Count > 0)
                    await FileIO.WriteLinesAsync(pendingFile, moreFails);
                else
                    await pendingFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }

        internal static async Task<InstalledFont> LoadFromFileAsync(StorageFile file)
        {
            StorageFolder folder = await ImportFolder.CreateFolderAsync(TEMP, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
            StorageFile localFile = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

            Dictionary<string, InstalledFont> resultList = new Dictionary<string, InstalledFont>();
            foreach (CanvasFontFace font in GetFontFacesFromFile(localFile))
                AddFont(resultList, font, localFile);

            if (resultList.Count > 0)
                return resultList.First().Value;

            return null;
        }
    }
}