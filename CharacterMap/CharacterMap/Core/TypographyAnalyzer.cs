using Microsoft.Graphics.Canvas.Text;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using CharacterMapCX;
using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using CharacterMap.Models;

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

            if (analysis.GlyphNames != null
                && analysis.GlyphNames.Count > 0)
            {
                PrepareSearchMap(variant, analysis.GlyphNames.ToList());
            }

            // TODO : Parse Design & Script Language Tags
            // if (!string.IsNullOrWhiteSpace(analysis.DesignLanguages))
            // {
            //     var langs = analysis.DesignLanguages.Split(',');
            // }

            return analysis;
        }

        private static void PrepareSearchMap(FontVariant variant, List<GlyphNameMap> names)
        {
            if (variant.SearchMap == null)
            {
                var idxs = variant.GetIndexes();
                var rng = variant.FontFace.GetGlyphIndices(idxs);

                Dictionary<Character, GlyphNameMap> map = new Dictionary<Character, GlyphNameMap>();

                var list = variant.GetCharacters();
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    var mapping = names[rng[i]];
                    mapping.Name = mapping.Name.Replace("-", " ").Replace("_", " ");
                    map.Add(c, mapping);
                }

                variant.SearchMap = map;
            }
        }
    }
}
