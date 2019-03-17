using System.Collections.Generic;
using System.Linq;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI.Text;

namespace CharacterMap.Core
{
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

        public static InstalledFont CreateDefault()
        {
            InstalledFont font = new InstalledFont()
            {
                Name = "",
                HasImportedFiles = false,
            };

            return font;
        }
        
    }
}
