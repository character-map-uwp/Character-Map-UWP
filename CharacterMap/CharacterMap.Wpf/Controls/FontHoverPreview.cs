using System.Windows;
using System.Windows.Controls;
using CharacterMap.Wpf.Models;
using CharacterMap.Wpf.Services;

namespace CharacterMap.Wpf.Controls;

public sealed class FontHoverPreview : WrapPanel
{
    public static readonly DependencyProperty FontProperty = DependencyProperty.Register(nameof(Font), typeof(FontEntry), typeof(FontHoverPreview), new PropertyMetadata(null, Changed));
    public FontEntry? Font { get => (FontEntry?)GetValue(FontProperty); set => SetValue(FontProperty, value); }
    public FontHoverPreview() { Width = 336; Loaded += (_, _) => Refresh(); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FontHoverPreview { IsLoaded: true } preview) preview.Refresh();
    }
    private void Refresh()
    {
        Children.Clear();
        if (Font is not { } font) return;
        var face = font.GlyphTypeface;
        var map = face.CharacterToGlyphMap;
        var color = ColorGlyphService.ForFace(face);
        int[] emoji = [0x1F602, 0x1F60D, 0x1F62D, 0x1F44D, 0x1F48B, 0x1F431, 0x1F989, 0x1F33A, 0x1F332, 0x1F353, 0x1F355, 0x1F382, 0x1F3E0, 0x1F684, 0x1F692, 0x1F6EB, 0x1F9F3, 0x1F3F0];
        var colored = emoji.Where(cp => map.TryGetValue(cp, out var id) && color.GetLayers(id) != null).ToArray();
        var sample = colored.Length > 0 ? colored : "AaBbCcDdEeFf0123456789".Select(c => (int)c).Where(map.ContainsKey).ToArray();
        if (sample.Length < 6) sample = map.Where(p => p.Key >= 0x21 && p.Value != 0).OrderBy(p => p.Key).Take(18).Select(p => p.Key).ToArray();
        foreach (int cp in sample.Take(18))
        {
            var glyph = new DirectText { GlyphTypeface = face, CodePoint = cp, Width = 56, Height = 56, FontSize = 38, Padding = new Thickness(4) };
            glyph.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            Children.Add(glyph);
        }
    }
}
