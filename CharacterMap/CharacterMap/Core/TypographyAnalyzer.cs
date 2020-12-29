using Microsoft.Graphics.Canvas.Text;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using CharacterMapCX;
using System.Diagnostics;

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
    }

    public class TypographyHandler : ICanvasTextRenderer
    {
        IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript>> _analyzedScript { get; }

        public List<TypographyFeatureInfo> TypographyOptions { get; }

        public TypographyHandler(string text)
        {
            var textAnalyzer = new CanvasTextAnalyzer(text, CanvasTextDirection.TopToBottomThenLeftToRight);
            _analyzedScript = textAnalyzer.GetScript();

            TypographyOptions = new List<TypographyFeatureInfo>
            {
                new TypographyFeatureInfo(CanvasTypographyFeatureName.None)
            };
        }

        private CanvasAnalyzedScript GetScript(uint textPosition)
        {
            foreach (KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript> range in _analyzedScript)
            {
                if (textPosition >= range.Key.CharacterIndex && textPosition < range.Key.CharacterIndex + range.Key.CharacterCount)
                {
                    return range.Value;
                }
            }

            return _analyzedScript[_analyzedScript.Count - 1].Value;
        }

        public void DrawGlyphRun(
            Vector2 position,
            CanvasFontFace fontFace,
            float fontSize,
            CanvasGlyph[] glyphs,
            bool isSideways,
            uint bidiLevel,
            object brush,
            CanvasTextMeasuringMode measuringMode,
            string locale,
            string textString,
            int[] clusterMapIndices,
            uint textPosition,
            CanvasGlyphOrientation glyphOrientation)
        {
            var script = GetScript(textPosition);

            CanvasTypographyFeatureName[] features = fontFace.GetSupportedTypographicFeatureNames(script);
            foreach (var featureName in features)
            {
                TypographyFeatureInfo featureInfo = new TypographyFeatureInfo(featureName);
                if (!TypographyOptions.Contains(featureInfo))
                {
                    TypographyOptions.Add(featureInfo);
                }
            }
        }

        public void DrawStrikethrough(
            Vector2 position,
            float strikethroughWidth,
            float strikethroughThickness,
            float strikethroughOffset,
            CanvasTextDirection textDirection,
            object brush,
            CanvasTextMeasuringMode measuringMode,
            string locale,
            CanvasGlyphOrientation glyphOrientation)
        {
        }

        public void DrawUnderline(
            Vector2 position,
            float underlineWidth,
            float underlineThickness,
            float underlineOffset,
            float runHeight,
            CanvasTextDirection textDirection,
            object brush,
            CanvasTextMeasuringMode measuringMode,
            string locale,
            CanvasGlyphOrientation glyphOrientation)
        {
        }

        public void DrawInlineObject(
            Vector2 baselineOrigin,
            ICanvasTextInlineObject inlineObject,
            bool isSideways,
            bool isRightToLeft,
            object brush,
            CanvasGlyphOrientation glyphOrientation)
        {
        }

        public float Dpi => 96;
        public bool PixelSnappingDisabled => false;
        public Matrix3x2 Transform => System.Numerics.Matrix3x2.Identity;
    }
}
