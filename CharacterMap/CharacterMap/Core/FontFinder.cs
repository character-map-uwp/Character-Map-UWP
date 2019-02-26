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
    public class FontFinder
    {
        public static CanvasFontSet FontCollection { get; set; }

        private static List<InstalledFont> _fonts { get; set; }

        public static async Task LoadFontsAsync()
        {
            FontCollection = CanvasFontSet.GetSystemFontSet();
            var familyCount = FontCollection.Fonts.Count;

            Dictionary<string, InstalledFont> fontList = new Dictionary<string, InstalledFont>();

            void AddFont(CanvasFontFace fontFace, StorageFile file = null)
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
                        var variant = new FontVariant(fontFace);

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
                            Variants = new List<FontVariant> { new FontVariant(fontFace) }
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

            for (var i = 0; i < familyCount; i++)
            {
                try
                {
                    CanvasFontFace f = FontCollection.Fonts[i];
                    AddFont(f);
                }
                catch (Exception)
                {
                    // Corrupted font files throw an exception
                }
            }

            foreach (var file in (await ApplicationData.Current.TemporaryFolder.GetFilesAsync()).OfType<StorageFile>())
            {
                foreach (var font in await LoadFontsAsync(file))
                    AddFont(font, file);
            }

            _fonts = fontList.OrderBy(f => f.Key).Select(f =>
            {
                f.Value.Variants = f.Value.Variants.OrderBy(v => v.FontFace.Weight.Weight).ToList();
                return f.Value;
            }).ToList();
        }

        public static List<InstalledFont> GetFonts()
        {
            return _fonts;
        }

        private static Task<List<CanvasFontFace>> LoadFontsAsync(StorageFile file)
        {
            return Task.Run(() =>
            {
                using (CanvasFontSet set = new CanvasFontSet(new Uri(GetAppPath(file))))
                {
                    return set.Fonts.ToList();
                }
            });
        }

        private static string GetAppPath(StorageFile file)
        {
            return $"ms-appdata:///temp/{file.Name}";
        }

        internal static async Task<bool> ImportFontsAsync(IReadOnlyList<IStorageItem> items)
        {
            bool import = false;
            foreach (var item in items.OfType<StorageFile>())
            {
                if (!item.IsAvailable)
                    continue;

                if (item.FileType.ToUpper().EndsWith("TTF") 
                    || item.FileType.ToUpper().EndsWith("OTF")
                    || item.FileType.ToUpper().EndsWith("CCF"))
                {
                    StorageFile fontFile = await item.CopyAsync(ApplicationData.Current.TemporaryFolder);
                    import = true;
                }
            }

            if (import)
            {
                await LoadFontsAsync();
            }

            return import;
        }
    }
}