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

    public static List<LigatureGroup> GetLigatures(CMFontFace fontFace)
    {
        IReadOnlyList<DWriteLigatureFeature> rawFeatures = DirectWrite.GetLigatures(fontFace.Face);
        if (rawFeatures is null || rawFeatures.Count == 0)
            return [];

        List<LigatureGroup> groups = [];

        foreach (DWriteLigatureFeature rawFeature in rawFeatures)
        {
            CanvasTypographyFeatureName feature = (CanvasTypographyFeatureName)rawFeature.FeatureTag;
            List<LigatureModel> ligatures = [];

            foreach (DWriteLigature rawLig in rawFeature.Ligatures)
            {
                List<LigatureComponent> components = [];
                foreach (ushort compGlyphId in rawLig.ComponentGlyphs)
                {
                    string ch = fontFace.TryGetCharacterForGlyph(compGlyphId, out Character mappedChar)
                        ? mappedChar.Char
                        : null;

                    components.Add(new(compGlyphId, ch));
                }

                ligatures.Add(new(rawLig.LigatureGlyph, components, feature));
            }

            if (ligatures.Count > 0)
                groups.Add(new(rawFeature.FeatureName, feature, ligatures));
        }

        return groups;
    }

    /// <summary>
    /// Returns a list of Typographic Variations for a character supported by the font,
    /// including whether the variation glyph is mapped to a character in the font face.
    /// </summary>
    public static List<TypographyVariation> GetCharacterVariations(CMFontFace fontFace, Character character)
    {
        List<TypographyVariation> supported = [TypographyVariation.None];

        if (fontFace.HasXamlTypographyFeatures)
        {
            CanvasTextAnalyzer textAnalyzer = new(character.Char, CanvasTextDirection.TopToBottomThenLeftToRight);
            KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript> analyzed = textAnalyzer.GetScript().First();

            CanvasGlyph[] glyphs = textAnalyzer.GetGlyphs(analyzed.Key, fontFace.FontFace, 24, false, false, analyzed.Value);
            int baseGlyphIndex = glyphs.Length > 0 ? glyphs[0].Index : -1;

            NativeInterop interop = Utils.GetInterop();

            foreach (TypographyFeatureInfo feature in fontFace.XamlTypographyFeatures)
            {
                if (feature == TypographyFeatureInfo.None)
                    continue;

                bool[] results = fontFace.FontFace.GetTypographicFeatureGlyphSupport(analyzed.Value, feature.Feature, glyphs);

                if (results.Any(r => r))
                {
                    TypographyVariation variation = new() { Feature = feature };

                    int variantGlyphIndex = interop.GetTypographicGlyph(fontFace.Face, character.Char, feature.Feature);

                    if (variantGlyphIndex > 0 && variantGlyphIndex != baseGlyphIndex)
                    {
                        if (fontFace.TryGetCharacterForGlyph(variantGlyphIndex, out Character mappedChar)
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
    /// <param name="model"></param>
    /// <returns></returns>
    public static FontAnalysis Analyze(FaceAnalysisModel model, bool loadGlyphNames = true)
    {
        FontAnalysis analysis = new (model.Face.Face);
        if (loadGlyphNames && analysis.HasGlyphNames)
            PrepareSearchMap(model, analysis.GlyphNameMappings);
        return analysis;
    }

    public static FontAnalysis QuickAnalyze(CMFontFace fontFace)
    {
        return new(fontFace.Face);
    }

    public static void PrepareSearchMap(FaceAnalysisModel model, FontAnalysis a)
    {
        if (model.SearchMap is null && a.HasGlyphNames)
            PrepareSearchMap(model, a.GlyphNameMappings);
    }

    private static void PrepareSearchMap(FaceAnalysisModel model, IReadOnlyDictionary<int, string> names)
    {
        if (model.SearchMap == null)
        {
            uint[] uni = model.Face.GetGlyphUnicodeIndexes();
            int[] gly = model.Face.Face.GetGlyphIndices(uni);
            IReadOnlyList<Character> chars = model.Face.GetCharacters();
            Dictionary<Character, string> map = new();

            for (int i = 0; i < chars.Count; i++)
            {
                Character c = chars[i];
                if (names.TryGetValue(gly[i], out string mapping) && !string.IsNullOrEmpty(mapping))
                    map.Add(c, mapping);
            }

            model.SearchMap = map;
        }
    }
}
