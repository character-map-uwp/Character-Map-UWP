using System.Collections.Generic;
using System.Linq;
using SharpDX.DirectWrite;
using FontFamily = Windows.UI.Xaml.Media.FontFamily;

namespace CharacterMap.Core
{
    public class InstalledFont
    {
        public string Name { get; set; }

        public FontFamily XamlFontFamily => new FontFamily(Name);

        public int FamilyIndex { get; set; }

        public bool IsSymbolFont { get; set; }

        public int Index { get; set; }

        private List<Character> Characters { get; set; }

        public string FontWeight { get; set; }

        public InstalledFont()
        {
            Characters = new List<Character>();
        }

        public List<Character> GetCharacters()
        {
            if (!Characters.Any())
            {
                var fontFamily = FontFinder.FontCollection.GetFontFamily(FamilyIndex);
                using (var font = fontFamily.GetFont(Index))
                {
                    var characters = new List<Character>();
                    var count = 65536 * 4 - 1;
                    for (var i = 0; i < count; i++)
                    {
                        if (font.HasCharacter(i))
                        {
                            string character = char.ConvertFromUtf32(i);

                            characters.Add(new Character
                            {
                                Char = character,
                                UnicodeIndex = i
                            });
                        }
                    }

                    Characters = characters;
                    return characters;
                } 
            }

            return Characters;
        }
    }
}
