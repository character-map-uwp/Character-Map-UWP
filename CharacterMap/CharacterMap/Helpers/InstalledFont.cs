using System.Collections.Generic;
using System.Globalization;
using SharpDX.DirectWrite;

namespace CharacterMap.Core
{
    public class InstalledFont
    {
        public string Name { get; set; }

        public int FamilyIndex { get; set; }

        public bool IsSymbolFont { get; set; }

        public int Index { get; set; }

        public static List<InstalledFont> GetFonts()
        {
            var fontList = new List<InstalledFont>();

            var factory = new Factory();
            var fontCollection = factory.GetSystemFontCollection(false);
            var familyCount = fontCollection.FontFamilyCount;

            for (int i = 0; i < familyCount; i++)
            {
                var fontFamily = fontCollection.GetFontFamily(i);
                var familyNames = fontFamily.FamilyNames;
                int index;

                if (!familyNames.FindLocaleName(CultureInfo.CurrentCulture.Name, out index))
                {
                    if (!familyNames.FindLocaleName("en-us", out index))
                    {
                        index = 0;
                    }
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

            return fontList;
        }

        public List<Character> GetCharacters()
        {
            var factory = new Factory();
            var fontCollection = factory.GetSystemFontCollection(false);
            var fontFamily = fontCollection.GetFontFamily(FamilyIndex);

            var font = fontFamily.GetFont(Index);

            var characters = new List<Character>();
            var count = 65535;
            for (var i = 0; i < count; i++)
            {
                if (font.HasCharacter(i))
                {
                    characters.Add(new Character()
                    {
                        Char = char.ConvertFromUtf32(i),
                        UnicodeIndex = i
                    });
                }
            }

            return characters;
        }
    }


    public class Character
    {
        public string Char { get; set; }

        public int UnicodeIndex { get; set; }

        public string UnicodeString
        {
            get
            {
                return "U+" + UnicodeIndex.ToString("x").ToUpper();
            }
        }

        public string XAMLString
        {
            get
            {
                return "&#x" + UnicodeIndex.ToString("x").ToUpper();
            }
        }
    }
}
