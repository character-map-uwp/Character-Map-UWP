using System.Collections.Generic;
using System.Globalization;
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

        public string PreferredName { get; }

        public IReadOnlyList<Character> Characters { get; private set; }

        public double CharacterHash { get; private set; }

        public FontVariant(CanvasFontFace face)
        {
            FontFace = face;
            Characters = new List<Character>();

            if (!face.FaceNames.TryGetValue(CultureInfo.CurrentCulture.Name, out string name))
            {
                if (!face.FaceNames.TryGetValue("en-us", out name))
                {
                    if (face.FaceNames.Any())
                        name = face.FaceNames.FirstOrDefault().Value;
                    else
                        name = Utils.GetVariantDescription(face);
                }
            }

            PreferredName = name;
        }

        public override string ToString()
        {
            return PreferredName;
        }

        public IReadOnlyList<Character> GetCharacters()
        {
            if (Characters.Count == 0)
            {
                var characters = new List<Character>();

                foreach (var range in FontFace.UnicodeRanges)
                {
                    CharacterHash += range.First;
                    CharacterHash += range.Last;

                    for (uint i = range.First; i <= range.Last; i++)
                    {
                        characters.Add(new Character
                        {
                            Char = char.ConvertFromUtf32((int)i),
                            UnicodeIndex = (int)i
                        });
                    }
                }
                Characters = characters;
                return characters;
            }

            return Characters;
        }
    }


    public class InstalledFont
    {
        public string Name { get; set; }

        public CanvasFontFace FontFace { get; set; }

        public bool IsSymbolFont { get; set; }

        public List<FontVariant> Variants { get; set; }

        public bool HasVariants => Variants.Count > 1;

        public bool HasImportedFiles { get; set; }

        public InstalledFont()
        {
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

        
    }
}
