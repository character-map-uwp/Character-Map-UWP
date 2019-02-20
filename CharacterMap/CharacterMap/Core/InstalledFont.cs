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

        public FontFamily XamlFontFamily { get; set; }

        public FontVariant(CanvasFontFace face)
        {
            FontFace = face;
        }

        public override string ToString()
        {
            return Utils.GetVariantDescription(FontFace);
        }
    }


    public class InstalledFont
    {
        public string Name { get; set; }

        public CanvasFontFace FontFace { get; set; }

        public bool IsSymbolFont { get; set; }

        private List<Character> Characters { get; set; }

        public List<FontVariant> Variants { get; set; }

        public InstalledFont()
        {
            Characters = new List<Character>();
        }

        public FontVariant DefaultVariant
        {
            get
            {
                return Variants.FirstOrDefault(v => v.FontFace.Weight.Weight == FontWeights.Normal.Weight && v.FontFace.Style == FontStyle.Normal && v.FontFace.Stretch == FontStretch.Normal) 
                    ?? Variants.FirstOrDefault(v => v.FontFace.Weight.Weight == FontWeights.Normal.Weight && v.FontFace.Style == FontStyle.Normal)
                    ?? Variants.FirstOrDefault(v => v.FontFace.Weight.Weight == FontWeights.Normal.Weight && v.FontFace.Stretch == FontStretch.Normal)
                    ?? Variants.FirstOrDefault(v => v.FontFace.Weight.Weight == FontWeights.Normal.Weight)
                    ?? Variants[0];
            }
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
