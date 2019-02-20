using System;
using System.Collections.Generic;
using System.Globalization;
using SharpDX.DirectWrite;

namespace CharacterMap.Core
{
    public class FontFinder
    {
        public static FontCollection FontCollection { get; set; }

        public static List<InstalledFont> GetFonts()
        {
            var fontList = new List<InstalledFont>();

            using (var factory = new Factory())
            {
                FontCollection = factory.GetSystemFontCollection(false);
                var familyCount = FontCollection.FontFamilyCount;

                for (var i = 0; i < familyCount; i++)
                {
                    try
                    {
                        using (var fontFamily = FontCollection.GetFontFamily(i))
                        {
                            var familyNames = fontFamily.FamilyNames;

                            if (!familyNames.FindLocaleName(CultureInfo.CurrentCulture.Name, out var index))
                            {
                                familyNames.FindLocaleName("en-us", out index);
                            }

                            if (index >= 0)
                            {
                                var name = familyNames.GetString(index);

                                using (var font = fontFamily.GetFont(index))
                                {
                                    fontList.Add(new InstalledFont
                                    {
                                        Name = name,
                                        FamilyIndex = i,
                                        Index = index,
                                        IsSymbolFont = font.IsSymbolFont,
                                        FontWeight = font.Weight.ToString(),
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Corrupted font files throw an exception
                    }
                }
            }

            return fontList;
        }
    }
}