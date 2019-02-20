using System.Collections.Generic;
using System.Linq;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI.Text;
using FontFamily = Windows.UI.Xaml.Media.FontFamily;

namespace CharacterMap.Core
{
    public class FontVariant
    {
        public CanvasFontFace FontFace { get; }

        public FontWeight Weight { get; }

        public FontStyle Style { get; }

        public FontVariant(CanvasFontFace face)
        {
            FontFace = face;
        }

        public override string ToString()
        {
            return $"{FontFace.Weight.Weight} {FontFace.Style} {FontFace.Stretch}";
        }
    }


    public class InstalledFont
    {
        public string Name { get; set; }

        public FontFamily XamlFontFamily => new FontFamily(Name);

        public CanvasFontFace FontFace { get; set; }

        public bool IsSymbolFont { get; set; }

        private List<Character> Characters { get; set; }

        public List<FontVariant> Variants { get; set; }

        public InstalledFont()
        {
            Characters = new List<Character>();
        }

        public List<Character> GetCharacters()
        {
            if (Characters.Count == 0)
            {
                var characters = new List<Character>();

                foreach (var range in FontFace.UnicodeRanges)
                {
                    for (uint i = range.First; i <= range.Last; i++)
                    {
                        characters.Add(new Character
                        {
                            Char = char.ConvertFromUtf32((int)i),
                            UnicodeIndex = (int)i
                        });
                    }
                }

                //uint count = 65536 * 4 - 1;
                //for (uint i = 0; i < count; i++)
                //{
                //    if (FontFace.HasCharacter(i))
                //    {
                            //characters.Add(new Character
                            //{
                            //    Char = char.ConvertFromUtf32((int)i),
                            //    UnicodeIndex = (int)i
                            //});
                //    }
                //}

                Characters = characters;
                return characters;
            }

            return Characters;
        }
    }
}
