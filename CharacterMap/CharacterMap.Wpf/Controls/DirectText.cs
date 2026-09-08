using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CharacterMap.Wpf.Controls;

/// <summary>Renders the selected face's actual glyph outline, including supplementary-plane glyphs, without font fallback.</summary>
public sealed class DirectText : Control
{
    public static readonly DependencyProperty GlyphTypefaceProperty = DependencyProperty.Register(nameof(GlyphTypeface), typeof(GlyphTypeface), typeof(DirectText), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateGeometry));
    public static readonly DependencyProperty CodePointProperty = DependencyProperty.Register(nameof(CodePoint), typeof(int), typeof(DirectText), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateGeometry));
    public static readonly DependencyProperty FitToBoundsProperty = DependencyProperty.Register(nameof(FitToBounds), typeof(bool), typeof(DirectText), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public GlyphTypeface? GlyphTypeface { get => (GlyphTypeface?)GetValue(GlyphTypefaceProperty); set => SetValue(GlyphTypefaceProperty, value); }
    public int CodePoint { get => (int)GetValue(CodePointProperty); set => SetValue(CodePointProperty, value); }
    public bool FitToBounds { get => (bool)GetValue(FitToBoundsProperty); set => SetValue(FitToBoundsProperty, value); }
    private Geometry? _outline;
    private static void InvalidateGeometry(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DirectText)d)._outline = null;
    public Geometry? GetOutline()
    {
        if (_outline != null) return _outline;
        if (GlyphTypeface?.CharacterToGlyphMap.TryGetValue(CodePoint, out ushort glyph) != true) return null;
        _outline = GlyphTypeface.GetGlyphOutline(glyph, 100, 100);
        if (_outline.CanFreeze) _outline.Freeze();
        return _outline;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var geometry = GetOutline();
        if (geometry == null || geometry.Bounds.IsEmpty || geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0) return;
        var bounds = geometry.Bounds;
        double availableWidth = Math.Max(1, ActualWidth - Padding.Left - Padding.Right);
        double availableHeight = Math.Max(1, ActualHeight - Padding.Top - Padding.Bottom);
        double scale = Math.Min(availableWidth / bounds.Width, availableHeight / bounds.Height);
        if (!FitToBounds) scale = Math.Min(scale, FontSize / 100);
        dc.PushTransform(new TranslateTransform(Padding.Left + (availableWidth - bounds.Width * scale) / 2 - bounds.X * scale,
            Padding.Top + (availableHeight - bounds.Height * scale) / 2 - bounds.Y * scale));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(Foreground, null, geometry);
        dc.Pop(); dc.Pop();
    }
}
