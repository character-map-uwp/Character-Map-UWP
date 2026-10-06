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
            string tag = DirectWrite.GetFeatureTag(rawFeature.FeatureTag);
            string title = string.IsNullOrEmpty(rawFeature.FeatureName) || rawFeature.FeatureName == tag ? tag : $"{rawFeature.FeatureName} ({tag})";
            CanvasTypographyFeatureName feature = (CanvasTypographyFeatureName)rawFeature.FeatureTag;
            List<LigatureModel> ligatures = [];

            foreach (DWriteLigature rawLig in rawFeature.Ligatures)
            {
                List<LigatureComponent> components = [];
                foreach (ushort compGlyphId in rawLig.ComponentGlyphs)
                {
                    bool hasChar = fontFace.TryGetCharacterForGlyph(compGlyphId, out Character mappedChar);
                    string ch = hasChar ? mappedChar.Char : null;
                    uint? uniIndex = hasChar ? mappedChar.UnicodeIndex : null;

                    string tip = null;
                    if (hasChar)
                    {
                        string desc = GlyphService.GetCharacterDescription(mappedChar.UnicodeIndex, fontFace);
                        tip = !string.IsNullOrWhiteSpace(desc)
                            ? $"{desc}\r\nUnicode: U+{mappedChar.UnicodeIndex:X4}\r\nGlyph #{compGlyphId}"
                            : $"Unicode: U+{mappedChar.UnicodeIndex:X4}\r\nGlyph #{compGlyphId}";
                    }
                    else
                        tip = $"Glyph #{compGlyphId}";

                    components.Add(new(compGlyphId, ch, uniIndex, tip));
                }

                string ligName = null;
                if (fontFace.TryGetCharacterForGlyph((ushort)rawLig.LigatureGlyph, out Character ligChar))
                {
                    string desc = GlyphService.GetCharacterDescription(ligChar.UnicodeIndex, fontFace);
                    ligName = !string.IsNullOrWhiteSpace(desc) ? desc : ligChar.Char;
                }

                ligatures.Add(new(rawLig.LigatureGlyph, components, feature, ligName));
            }

            if (ligatures.Count > 0)
                groups.Add(new(title, tag, rawFeature.FeatureName, feature, ligatures));
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
            int baseGlyphIndex = character is GlyphCharacter gc
                ? (int)gc.GlyphIndex
                : fontFace.Face.GetGlyphIndice(character.UnicodeIndex);

            NativeInterop interop = Utils.GetInterop();
            List<CanvasTypographyFeatureName> featuresToCheck = fontFace.XamlTypographyFeatures
                .Where(f => f != TypographyFeatureInfo.None)
                .Select(f => f.Feature)
                .ToList();

            IReadOnlyList<CanvasTypographyFeatureName> supportedFeatures = DirectWrite.GetSupportedTypographicFeatures(
                fontFace.Face, character.Char, featuresToCheck);

            foreach (CanvasTypographyFeatureName featureTag in supportedFeatures)
            {
                TypographyFeatureInfo feature = fontFace.XamlTypographyFeatures.FirstOrDefault(f => f.Feature == featureTag);
                if (feature is null)
                    continue;

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
            List<CanvasTypographyFeatureName> featuresToCheck = font.XamlTypographyFeatures
                .Where(f => f != TypographyFeatureInfo.None)
                .Select(f => f.Feature)
                .ToList();

            IReadOnlyList<CanvasTypographyFeatureName> supportedFeatures = DirectWrite.GetSupportedTypographicFeatures(
                font.Face, character.Char, featuresToCheck);

            foreach (CanvasTypographyFeatureName featureTag in supportedFeatures)
            {
                if (font.XamlTypographyFeatures.FirstOrDefault(f => f.Feature == featureTag) is { } feature)
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
        analysis.ResetVariableAxis();
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
