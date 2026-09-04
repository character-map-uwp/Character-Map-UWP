using CharacterMap.Models;
using CharacterMap.ViewModels;
using Microsoft.Graphics.Canvas.Text;

namespace CharacterMap.Core;

public static class TypographyAnalyzer
{
    public static List<TypographyFeatureInfo> GetSupportedTypographyFeatures(CMFontFace variant)
    {
        var features = DirectWrite.GetSupportedTypography(variant.Face)?.Values?.ToList();

        if (features is null)
            return [];

        var list = features.Select(f => new TypographyFeatureInfo((CanvasTypographyFeatureName)f)).OrderBy(f => f.DisplayName).ToList();
        return list;
    }

    /// <summary>
    /// Returns a list of Typographic Variations for a character supported by the font,
    /// including whether the variation glis mapped to a character in the font face.
    /// </summary>
    public static List<TypographyVariation> GetCharacterVariations(CMFontFace font, Character character)
    {
        List<TypographyVariation> supported = [TypographyVariation.None];

        if (font.HasXamlTypographyFeatures)
        {
            CanvasTextAnalyzer textAnalyzer = new(character.Char, CanvasTextDirection.TopToBottomThenLeftToRight);
            KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript> analyzed = textAnalyzer.GetScript().First();

            CanvasGlyph[] glyphs = textAnalyzer.GetGlyphs(analyzed.Key, font.FontFace, 24, false, false, analyzed.Value);
            int baseGlyphIndex = glyphs.Length > 0 ? glyphs[0].Index : -1;

            NativeInterop interop = Utils.GetInterop();

            foreach (TypographyFeatureInfo feature in font.XamlTypographyFeatures)
            {
                if (feature == TypographyFeatureInfo.None)
                    continue;

                bool[] results = font.FontFace.GetTypographicFeatureGlyphSupport(analyzed.Value, feature.Feature, glyphs);

                if (results.Any(r => r))
                {
                    TypographyVariation variation = new() { Feature = feature };

                    int variantGlyphIndex = interop.GetTypographicGlyph(font.Face, character.Char, feature.Feature);

                    if (variantGlyphIndex > 0 && variantGlyphIndex != baseGlyphIndex)
                    {
                        if (font.TryGetCharacterForGlyph(variantGlyphIndex, out Character mappedChar)
                            && mappedChar.UnicodeIndex != character.UnicodeIndex)
                            variation.FaceCharacterMapping = (int)mappedChar.UnicodeIndex;
                    }

                    supported.Add(variation);
                }
            }
        }

        return supported;
    }

    /// <summary>
    /// Returns a list of Typographic Variants for a character supported by the font.
    /// </summary>
    public static List<TypographyFeatureInfo> GetCharacterVariants(CMFontFace font, Models.Character character)
    {
        List<TypographyFeatureInfo> supported = [TypographyFeatureInfo.None];

        if (font.HasXamlTypographyFeatures)
        {
            CanvasTextAnalyzer textAnalyzer = new(character.Char, CanvasTextDirection.TopToBottomThenLeftToRight);
            KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript> analyzed = textAnalyzer.GetScript().First();

            var glyphs = textAnalyzer.GetGlyphs(analyzed.Key, font.FontFace, 24, false, false, analyzed.Value);

            foreach (var feature in font.XamlTypographyFeatures)
            {
                if (feature == TypographyFeatureInfo.None)
                    continue;

                bool[] results = font.FontFace.GetTypographicFeatureGlyphSupport(analyzed.Value, feature.Feature, glyphs);

                if (results.Any(r => r))
                    supported.Add(feature);
            }
        }

        return supported;
    }

    /// <summary>
    /// Creates a FontAnalysis object for a FontVariant and ensures the custom
    /// search map for the font is loaded
    /// </summary>
    /// <param name="variant"></param>
    /// <returns></returns>
    public static FontAnalysis Analyze(CMFontFace variant, bool loadGlyphNames = true)
    {
        FontAnalysis analysis = new(variant.Face);
        if (loadGlyphNames && analysis.HasGlyphNames)
            PrepareSearchMap(variant, analysis.GlyphNameMappings);
        return analysis;
    }

    public static void PrepareSearchMap(CMFontFace variant, FontAnalysis a)
    {
        if (variant.SearchMap is null && a.HasGlyphNames)
            PrepareSearchMap(variant, a.GlyphNameMappings);
    }

    private static void PrepareSearchMap(CMFontFace variant, IReadOnlyDictionary<int, string> names)
    {
        if (variant.SearchMap == null)
        {
            uint[] uni = variant.GetGlyphUnicodeIndexes();
            int[] gly = variant.Face.GetGlyphIndices(uni);
            IReadOnlyList<Character> chars = variant.GetCharacters();
            Dictionary<Character, string> map = new();

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
}
