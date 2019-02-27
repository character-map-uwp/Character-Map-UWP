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
        public static List<InstalledFont> Fonts { get; private set; }

        public static InstalledFont DefaultFont { get; private set; }

        public static async Task LoadFontsAsync()
        {
            Fonts = null;
            var systemFonts = CanvasFontSet.GetSystemFontSet();
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
                if (i == 0)
                {
                    DefaultFont = fontList.First().Value;
                }
            }

            /* Add imported fonts */
            var files = await ApplicationData.Current.LocalFolder.GetFilesAsync();
            foreach (var file in files.OfType<StorageFile>())
            {
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

                if (file.FileType.ToUpper().EndsWith("TTF")
                    || file.FileType.ToUpper().EndsWith("OTF"))
                {
                    // TODO : What do we do if a font with the same file name already exists?
                    if (ApplicationData.Current.LocalFolder.TryGetItemAsync(item.Name) == null)
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

        internal static async Task RemoveFontAsync(InstalledFont font)
        {
            Fonts = null;

            font.FontFace = null;
            var variants = font.Variants.Where(v => v.IsImported).ToList();
            foreach (var variant in variants)
            {
                font.Variants.Remove(variant);
                variant.Dispose();

                GC.Collect(); // required to prevent "File in use" exception

                if (await ApplicationData.Current.LocalFolder.TryGetItemAsync(variant.FileName)
                    is StorageFile file)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }

            await LoadFontsAsync();
        }
    }
}