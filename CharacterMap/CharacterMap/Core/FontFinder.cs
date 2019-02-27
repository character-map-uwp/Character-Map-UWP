using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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

        /* If we can't delete a font during a session, we mark it here */
        private static HashSet<string> _ignoredFonts { get; } = new HashSet<string>();

        private static StorageFolder ImportFolder => ApplicationData.Current.LocalFolder;

        public static List<InstalledFont> Fonts { get; private set; }

        public static InstalledFont DefaultFont { get; private set; }


        public static async Task LoadFontsAsync()
        {
            Fonts = null;
            
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

                    DefaultFont.DefaultVariant.SetAsDefault();
                }
            }
            

            var familyCount = systemFonts.Fonts.Count;
            Dictionary<string, InstalledFont> fontList = new Dictionary<string, InstalledFont>();

            /* 
             * Helper method for adding fonts. Needs to be called on 
             * UI Thread as we construct XAML FontFamily here 
             */
            void AddFont(CanvasFontFace fontFace, StorageFile file = null)
            {
                try
                {
                    var familyNames = fontFace.FamilyNames;
                    if (!familyNames.TryGetValue(CultureInfo.CurrentCulture.Name, out string key))
                    {
                        familyNames.TryGetValue("en-us", out key);
                    }

                    if (key != null)
                    {
                        if (fontList.TryGetValue(key, out InstalledFont font))
                        {
                            var variant = new FontVariant(fontFace, file);

                            if (file == null)
                                variant.XamlFontFamily = font.Variants[0].XamlFontFamily;
                            else
                            {
                                variant.XamlFontFamily = new Windows.UI.Xaml.Media.FontFamily($"{GetAppPath(file)}#{key}");
                                font.HasImportedFiles = true;
                            }

                            font.Variants.Add(variant);
                        }
                        else
                        {
                            var f = new InstalledFont
                            {
                                Name = key,
                                IsSymbolFont = fontFace.IsSymbolFont,
                                FontFace = fontFace,
                                Variants = new List<FontVariant> { new FontVariant(fontFace, file) }
                            };

                            if (file != null)
                            {
                                f.Variants[0].XamlFontFamily = new Windows.UI.Xaml.Media.FontFamily($"{GetAppPath(file)}#{key}");
                                f.HasImportedFiles = true;
                            }
                            else
                            {
                                f.Variants[0].XamlFontFamily = new Windows.UI.Xaml.Media.FontFamily(f.Name);
                            }

                            fontList[key] = f;
                        }
                    }
                }
                catch (Exception)
                {
                    // Corrupted font files throw an exception
                }
            }

            /* Add all system fonts */
            for (var i = 0; i < familyCount; i++)
            {
                AddFont(systemFonts.Fonts[i]);
            }

            /* Add imported fonts */
            var files = await ImportFolder.GetFilesAsync();
            foreach (var file in files.OfType<StorageFile>())
            {
                if (_ignoredFonts.Contains(file.Name))
                    continue;

                foreach (var font in GetFontFacesFromFile(file))
                    AddFont(font, file);
            }

            /* Order everything appropriately */
            Fonts = fontList.OrderBy(f => f.Key).Select(f =>
            {
                f.Value.Variants = f.Value.Variants.OrderBy(v => v.FontFace.Weight.Weight).ToList();
                return f.Value;
            }).ToList();
        }

        private static List<CanvasFontFace> GetFontFacesFromFile(StorageFile file)
        {
            using (CanvasFontSet set = new CanvasFontSet(new Uri(GetAppPath(file))))
            {
                return set.Fonts.ToList();
            }
        }

        private static string GetAppPath(StorageFile file)
        {
            return $"ms-appdata:///local/{file.Name}";
        }

        internal static async Task<FontImportResult> ImportFontsAsync(IReadOnlyList<IStorageItem> items)
        {
            List<StorageFile> _imported = new List<StorageFile>();
            List<StorageFile> _existing = new List<StorageFile>();
            List<(IStorageItem, string)> _invalid = new List<(IStorageItem, string)>();

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
                }

                if (file.FileType.ToUpper().EndsWith("TTF")
                    || file.FileType.ToUpper().EndsWith("OTF"))
                {
                    // TODO : What do we do if a font with the same file name already exists?
                    if (ImportFolder.TryGetItemAsync(item.Name) == null)
                    {
                        StorageFile fontFile = await file.CopyAsync(ApplicationData.Current.LocalFolder);
                        _imported.Add(fontFile);
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

            // TODO : Should we actually verify these are legit font files
            //        here by trying to open them?

            return new FontImportResult(_imported, _existing, _invalid);
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

    }
}