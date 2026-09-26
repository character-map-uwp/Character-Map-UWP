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

    public GlyphCharacter(ushort glyphIndex, int paletteIndex = -1, Color? color = null) : base(0)
    {
        GlyphIndex = glyphIndex;
        PaletteIndex = paletteIndex;
        Color = color;
    }
}
