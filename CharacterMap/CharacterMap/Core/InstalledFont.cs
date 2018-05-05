using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

                for (int i = 0; i < familyCount; i++)
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

                            bool isSymbolFont = fontFamily.GetFont(index).IsSymbolFont;

                            string name = familyNames.GetString(index);
                            fontList.Add(new InstalledFont()
                            {
                                Name = name,
                                FamilyIndex = i,
                                Index = index,
                                IsSymbolFont = isSymbolFont
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        // Corrupted font files throw an exception
                    }
                }
            }

            return fontList;
        }
    }

    public class InstalledFont
    {
        public string Name { get; set; }

        public int FamilyIndex { get; set; }

        public bool IsSymbolFont { get; set; }

        public int Index { get; set; }

        private List<Character> Characters { get; set; }

        public InstalledFont()
        {
            Characters = new List<Character>();
        }

        public List<Character> GetCharacters()
        {
            if (!Characters.Any())
            {
                var fontFamily = FontFinder.FontCollection.GetFontFamily(FamilyIndex);
                var font = fontFamily.GetFont(Index);

                var characters = new List<Character>();
                var count = 65536 * 4 - 1;
                for (var i = 0; i < count; i++)
                {
                    if (font.HasCharacter(i))
                    {
                        characters.Add(new Character
                        {
                            Char = char.ConvertFromUtf32(i),
                            UnicodeIndex = i
                        });
                    }
                }

                Characters = characters;
                return characters;
            }

            return Characters;
        }
    }

    public class Character
    {
        public string Char { get; set; }

        public int UnicodeIndex { get; set; }

        public string UnicodeString => "U+" + UnicodeIndex.ToString("x").ToUpper();
    }
}
