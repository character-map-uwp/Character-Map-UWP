using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI.Text;

namespace CharacterMap.Core
{
    public class FontFinder
    {
        public static CanvasFontSet FontCollection { get; set; }

        public static List<InstalledFont> GetFonts()
        {
            FontCollection = CanvasFontSet.GetSystemFontSet();
            var familyCount = FontCollection.Fonts.Count;

            Dictionary<string, InstalledFont> fontList = new Dictionary<string, InstalledFont>();

            for (var i = 0; i < familyCount; i++)
            {
                try
                {
                    CanvasFontFace fontFace = FontCollection.Fonts[i];
                    var familyNames = fontFace.FamilyNames;
                    if (!familyNames.TryGetValue(CultureInfo.CurrentCulture.Name, out string key))
                    {
                        familyNames.TryGetValue("en-us", out key);
                    }

                    if (key != null)
                    {
                        if (fontList.TryGetValue(key, out InstalledFont font))
                        {
                            // add weight?
                            font.Variants.Add(new FontVariant(fontFace));
                        }
                        else
                        {
                            fontList[key] = new InstalledFont
                            {
                                Name = key,
                                IsSymbolFont = fontFace.IsSymbolFont,
                                FontFace = fontFace,
                                Variants = new List<FontVariant> { new FontVariant(fontFace) }
                            };
                        }
                    }

                }
                catch (Exception)
                {
                    // Corrupted font files throw an exception
                }
            }

            return fontList.OrderBy(f => f.Key).Select(f => f.Value).ToList();
        }
    }
}