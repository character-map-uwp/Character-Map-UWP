using Windows.UI;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Models;

public class GlyphCharacter : Character
{
    public const int NoPaletteIndex = 0xFFFF;

    public ushort GlyphIndex { get; }

    public int PaletteIndex { get; }

    /// <summary>
    /// Default color specified by the Palette Index
    /// </summary>
    public Color? Color { get; }

    bool _valid = false;

    public override bool IsValidUnicode => _valid ? base.IsValidUnicode : false;

    public GlyphCharacter(ushort glyphIndex, int paletteIndex = -1, Color? color = null, uint? unicodeIndex = null) : base(unicodeIndex ?? uint.MaxValue)
    {
        GlyphIndex = glyphIndex;
        PaletteIndex = paletteIndex;
        Color = color;

        if (unicodeIndex is not null && unicodeIndex != uint.MaxValue)
            _valid = true;
    }

    public GlyphCharacter(ushort glyphIndex, uint unicodeIndex) : this(glyphIndex, -1, null, unicodeIndex) { }

    public GlyphCharacter(ushort glyphIndex, uint unicodeIndex, int paletteIndex, Color? color) : this(glyphIndex, paletteIndex, color, unicodeIndex) { }


    public bool CanCopy(FaceAnalysisModel model)
    {
        // If mapped directly to a character or a ligature we're all good.
        return (model.Face.TryGetCharacterForGlyph(GlyphIndex, out _) || model.TryGetLigature(GlyphIndex, out _));
    }

    public override string GetClipboardString(FaceAnalysisModel model)
    {
        if (model.TryGetLigature(GlyphIndex, out var ligature))
            return ligature.ClipboardText;

        if (model.Face.TryGetCharacterForGlyph(GlyphIndex, out Character character))
            return character.GetClipboardString(model);
        else 
            return $"[UNMAPPED GLYPH {GlyphIndex}]";
    }
}
