using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Humanizer;
using CharacterMapCX;

namespace CharacterMap.Core
{
    public static class TypographyAnalyzer
    {

        public static List<TypographyFeatureInfo> GetSupportedTypographyFeatures(FontVariant variant)
        {
            Dictionary<string, TypographyFeatureInfo> features = new Dictionary<string, TypographyFeatureInfo>();
            var analyzer = new CanvasTextAnalyzer(variant.GetCharString(), CanvasTextDirection.LeftToRightThenTopToBottom);
            {
                foreach (var script in analyzer.GetScript())
                {
                    foreach (var feature in variant.FontFace.GetSupportedTypographicFeatureNames(script.Value))
                    {
                        var info = new TypographyFeatureInfo(feature);
                        if (!features.ContainsKey(info.DisplayName))
                        {
                            features.Add(info.DisplayName, info);
                        }
                    }
                }
            }

            return features.Values.OrderBy(f => f.DisplayName).ToList();
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

    public class TypographyFeatureInfo
    {
        public CanvasTypographyFeatureName Feature { get; }

        public string DisplayName { get; }

        public TypographyFeatureInfo(CanvasTypographyFeatureName n)
        {
            Feature = n;

            if (IsNamedFeature(Feature))
            {
                DisplayName = Feature.Humanize().Transform(To.TitleCase);
            }
            else
            {
                //
                // For custom font features, we can produce the OpenType feature tag
                // using the feature name.
                //
                uint id = (uint)(Feature);
                DisplayName = DirectWrite.GetTagName(id);
            }
        }


        public override string ToString()
        {
            return DisplayName;
        }

        public override bool Equals(object obj)
        {
            if (obj is TypographyFeatureInfo other)
                return Feature == other.Feature;
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        bool IsNamedFeature(CanvasTypographyFeatureName name)
        {
            //
            // DWrite and Win2D support a pre-defined list of typographic features.
            // However, fonts are free to expose features outside of that list.
            // In fact, many built-in fonts have such custom features. 
            // 
            // These custom features are also accessible through Win2D, and 
            // are reported by GetSupportedTypographicFeatureNames.
            //

            return _allValues.Contains(name);
        }

        private static HashSet<CanvasTypographyFeatureName> _allValues { get; } = new HashSet<CanvasTypographyFeatureName>(
            Enum.GetValues(typeof(CanvasTypographyFeatureName)).Cast<CanvasTypographyFeatureName>());
    }
}
