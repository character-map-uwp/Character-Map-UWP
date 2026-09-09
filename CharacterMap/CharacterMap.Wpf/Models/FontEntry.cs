using System.Windows.Media;

namespace CharacterMap.Wpf.Models;

// Selector items require stable identity. A record's generated hash would include
// the lazy variant cache and change as a font becomes visible in the font list.
public sealed class FontEntry(
    string displayName,
    FontFamily family,
    GlyphTypeface glyphTypeface,
    string? sourcePath = null)
{
    public string DisplayName { get; } = displayName;
    public FontFamily Family { get; } = family;
    private FontFamily? _displayFamily;
    // Symbol fonts often have no Latin letters; render their names with the UI
    // font instead. Check the same normal face that the list TextBlock requests.
    public FontFamily DisplayFamily => _displayFamily ??= CanRenderName()
        ? Family : new FontFamily("Segoe UI");
    private bool CanRenderName()
    {
        var typeface = new Typeface(Family, System.Windows.FontStyles.Normal,
            System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
        return typeface.TryGetGlyphTypeface(out var face) && !face.Symbol
            && DisplayName.EnumerateRunes().All(rune =>
                face.CharacterToGlyphMap.TryGetValue(rune.Value, out ushort glyph) && glyph != 0);
    }
    public GlyphTypeface GlyphTypeface { get; } = glyphTypeface;
    public string? SourcePath { get; } = sourcePath;
    public string SourceLabel => SourcePath is null ? "系统字体" : SourcePath;
    public string Group => DisplayName.Length == 0 ? "#" : char.ToUpperInvariant(DisplayName[0]).ToString();
    private IReadOnlyList<FontVariant>? _variants;
    public IReadOnlyList<FontVariant> Variants => _variants ??= Family.GetTypefaces()
        .Select(t => t.TryGetGlyphTypeface(out var g) ? new FontVariant(g) : null)
        .OfType<FontVariant>().DistinctBy(v => v.Name).OrderBy(v => v.GlyphTypeface.Weight.ToOpenTypeWeight()).ToArray();
    public int VariantCount => Variants.Count;
    public override string ToString() => DisplayName;
}

public sealed record FontVariant(GlyphTypeface GlyphTypeface)
{
    public string Name => GlyphTypeface.FaceNames.Values.FirstOrDefault() ?? "Regular";
    public System.Windows.FontWeight Weight => GlyphTypeface.Weight;
    public System.Windows.FontStyle Style => GlyphTypeface.Style;
    public System.Windows.FontStretch Stretch => GlyphTypeface.Stretch;
    public override string ToString() => Name;
}
