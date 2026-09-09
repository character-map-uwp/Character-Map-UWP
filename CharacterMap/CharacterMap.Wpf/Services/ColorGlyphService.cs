using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CharacterMap.Wpf.Services;

/// <summary>OpenType COLR v0 layers (also allowed in v1), using CPAL's default palette.</summary>
public sealed class ColorGlyphService
{
    public sealed record Layer(ushort Glyph, Brush? Brush);
    private static readonly ConditionalWeakTable<GlyphTypeface, ColorGlyphService> Cache = new();
    private readonly Dictionary<ushort, Layer[]> _glyphs = new();
    public static ColorGlyphService ForFace(GlyphTypeface face) => Cache.GetValue(face, f =>
    {
        try { return f.FontUri.IsFile ? Parse(FontBinary.ReadFile(f.FontUri.LocalPath), f.GlyphCount) : new(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(); }
    });
    public IReadOnlyList<Layer>? GetLayers(ushort glyph) => _glyphs.GetValueOrDefault(glyph);

    public static ColorGlyphService Parse(byte[] data, int glyphCount)
    {
        var result = new ColorGlyphService();
        var r = new FontBinary(data);
        // A collection's active face index is not exposed by WPF. Never use another face's palette.
        if (r.U32(0) == 0x74746366) return result;
        if (r.U32(0) is not (0x00010000 or 0x4F54544F)) return result;
        int count = r.U16(4);
        r.Range(12, count * 16);
        byte[]? colr = null, cpal = null;
        for (int i = 0; i < count; i++)
        {
            int p = 12 + i * 16;
            string tag = r.Tag(p);
            if (tag is not ("COLR" or "CPAL")) continue;
            int start = r.Length(p + 8), length = r.Length(p + 12);
            r.Range(start, length);
            if (tag == "COLR") colr = data.AsSpan(start, length).ToArray();
            else cpal = data.AsSpan(start, length).ToArray();
        }
        if (colr == null || cpal == null) return result;
        var c = new FontBinary(colr); var palette = new FontBinary(cpal);
        if (c.U16(0) > 1 || palette.U16(0) > 1) return result;
        int entries = palette.U16(2), palettes = palette.U16(4), colors = palette.U16(6);
        if (palettes == 0) return result;
        palette.Range(12, palettes * 2);
        int colorStart = palette.Length(8), first = palette.U16(12);
        palette.Range(colorStart, colors * 4);
        if (first + entries > colors) throw new InvalidDataException("Invalid CPAL palette.");
        var brushes = new Brush[entries];
        for (int i = 0; i < entries; i++)
        {
            int p = colorStart + (first + i) * 4;
            var brush = new SolidColorBrush(Color.FromArgb(cpal[p + 3], cpal[p + 2], cpal[p + 1], cpal[p]));
            brush.Freeze(); brushes[i] = brush;
        }
        int bases = c.U16(2), baseStart = c.Length(4), layerStart = c.Length(8), layers = c.U16(12);
        c.Range(baseStart, bases * 6); c.Range(layerStart, layers * 4);
        int totalLayers = 0;
        for (int i = 0; i < bases; i++)
        {
            int p = baseStart + i * 6;
            ushort glyph = c.U16(p); int start = c.U16(p + 2), length = c.U16(p + 4);
            if (glyph >= glyphCount || start + length > layers) throw new InvalidDataException("Invalid COLR base glyph.");
            totalLayers += length;
            if (totalLayers > 1_000_000) throw new InvalidDataException("COLR layer expansion exceeds the supported limit.");
            if (length == 0) continue;
            var records = new Layer[length];
            for (int j = 0; j < length; j++)
            {
                int l = layerStart + (start + j) * 4;
                ushort id = c.U16(l), color = c.U16(l + 2);
                if (id >= glyphCount || (color != 0xFFFF && color >= entries)) throw new InvalidDataException("Invalid COLR layer.");
                records[j] = new(id, color == 0xFFFF ? null : brushes[color]);
            }
            if (!result._glyphs.TryAdd(glyph, records)) throw new InvalidDataException("Duplicate COLR glyph.");
        }
        return result;
    }
}
