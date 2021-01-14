using Microsoft.Graphics.Canvas.Text;
using System.Collections.Generic;
using System.Linq;
using CharacterMapCX;
using CharacterMap.Models;
using System.Globalization;

namespace CharacterMap.Core
{
    public static class TypographyAnalyzer
    {
        public static List<TypographyFeatureInfo> GetSupportedTypographyFeatures(FontVariant variant)
        {
            var features = DirectWrite.GetSupportedTypography(variant.FontFace).Values.ToList();
            var list = features.Select(f => new TypographyFeatureInfo((CanvasTypographyFeatureName)f)).OrderBy(f => f.DisplayName).ToList();
            return list;
        }

        /// <summary>
        /// Returns a list of Typographic Variants for a character supported by the font.
        /// </summary>
        public static List<TypographyFeatureInfo> GetCharacterVariants(FontVariant font, Models.Character character)
        {
            var textAnalyzer = new CanvasTextAnalyzer(character.Char, CanvasTextDirection.TopToBottomThenLeftToRight);
            KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript> analyzed = textAnalyzer.GetScript().First();

            List<TypographyFeatureInfo> supported = new List<TypographyFeatureInfo>
            {
                TypographyFeatureInfo.None
            };

            foreach (var feature in font.XamlTypographyFeatures)
            {
                if (feature == TypographyFeatureInfo.None)
                    continue;

                var glyphs = textAnalyzer.GetGlyphs(analyzed.Key, font.FontFace, 24, false, false, analyzed.Value);
                bool[] results = font.FontFace.GetTypographicFeatureGlyphSupport(analyzed.Value, feature.Feature, glyphs);

                if (results.Any(r => r))
                    supported.Add(feature);
            }

            return supported;
        }

        /// <summary>
        /// Creates a FontAnalysis object for a FontVariant and ensures the custom
        /// search map for the font is loaded
        /// </summary>
        /// <param name="variant"></param>
        /// <returns></returns>
        public static FontAnalysis Analyze(FontVariant variant)
        {
            var analysis = new FontAnalysis(variant.FontFace);

            if (analysis.HasGlyphNames)
            {
                PrepareSearchMap(variant, analysis.GlyphNameMappings);
            }
            return analysis;
        }

        private static void PrepareSearchMap(FontVariant variant, IReadOnlyDictionary<int, string> names)
        {
            if (variant.SearchMap == null)
            {
                uint[] uni = variant.GetGlyphUnicodeIndexes();
                int[] gly = variant.FontFace.GetGlyphIndices(uni);
                IReadOnlyList<Character> chars = variant.GetCharacters();

                Dictionary<Character, string> map = new Dictionary<Character, string>();

                for (int i = 0; i < chars.Count; i++)
                {
                    Character c = chars[i];
                    if (names.TryGetValue(gly[i], out string mapping) && !string.IsNullOrEmpty(mapping))
                    {
                        map.Add(c, mapping);
                    }
                }

                variant.SearchMap = map;
            }
        }

        private static string GetSantisedGlyphName(string name)
        {
            /*
             * Handle AGLFN mappings.
             * 'uXXXX' & 'uniXXXX' mappings should be ignored.
             * 
             * Older fonts may use AGLF names from older versions of the Adobe Glyph 
             * name mapping values, like 'afii' or 'commaaccent' that have been removed 
             * from the spec and are not in our listings.
             */

            // Currently the commented out are handled in C++ in PostTableReader.h, though leaving
            // here as we **may** move back to C# at some point.

            //if (name.StartsWith("uni") 
            //    && name.Length == 7
            //    && int.TryParse(name.Substring(3, 4), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out _))
            //    return null;

            //if (name.StartsWith('u')
            //    && (name.Length == 5 || name.Length == 6)
            //    && int.TryParse(name.Substring(1, name.Length - 1), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out _))
            //    return null;

            //if (name.StartsWith("afii"))
            //    return null;

            //if (name.Contains("commaaccent"))
            //    return null;

            // TODO : Replace these suffixes?
            // .smcp -> Small Capitals
            // .alt -> Alternate?
            // .sups -> Superscript
            // .sinf -> Scientific Inferior


            return name;
        }
    }
}
