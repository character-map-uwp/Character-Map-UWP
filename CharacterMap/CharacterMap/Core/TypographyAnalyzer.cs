using Microsoft.Graphics.Canvas.Text;

namespace CharacterMap.Core;

public static class TypographyAnalyzer
{
    public static List<TypographyFeatureInfo> GetSupportedTypographyFeatures(FontVariant variant)
    {
        var features = DirectWrite.GetSupportedTypography(variant.Face).Values.ToList();
        var list = features.Select(f => new TypographyFeatureInfo((CanvasTypographyFeatureName)f)).OrderBy(f => f.DisplayName).ToList();
        return list;
    }

    /// <summary>
    /// Returns a list of Typographic Variants for a character supported by the font.
    /// </summary>
    public static List<TypographyFeatureInfo> GetCharacterVariants(FontVariant font, Models.Character character)
    {
        CanvasTextAnalyzer textAnalyzer = new (character.Char, CanvasTextDirection.TopToBottomThenLeftToRight);
        KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript> analyzed = textAnalyzer.GetScript().First();

        List<TypographyFeatureInfo> supported = new ()
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
    public static FontAnalysis Analyze(FontVariant variant, bool loadGlyphNames = true)
    {
        FontAnalysis analysis = new (variant.Face);
        if (loadGlyphNames && analysis.HasGlyphNames)
            PrepareSearchMap(variant, analysis.GlyphNameMappings);
        return analysis;
    }

    public static void PrepareSearchMap(FontVariant variant, FontAnalysis a)
    {
        if (variant.SearchMap is null && a.HasGlyphNames)
            PrepareSearchMap(variant, a.GlyphNameMappings);
    }

    private static void PrepareSearchMap(FontVariant variant, IReadOnlyDictionary<int, string> names)
    {
        if (variant.SearchMap == null)
        {
            uint[] uni = variant.GetGlyphUnicodeIndexes();
            int[] gly = variant.Face.GetGlyphIndices(uni);
            IReadOnlyList<Character> chars = variant.GetCharacters();
            Dictionary<Character, string> map = new ();

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
