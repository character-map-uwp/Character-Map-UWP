using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CharacterMap.Wpf.Services;

namespace CharacterMap.Wpf.Controls;

/// <summary>Renders the selected face's outlines and COLR/CPAL layers without font fallback.</summary>
public sealed class DirectText : Control
{
    public static readonly DependencyProperty GlyphTypefaceProperty = DependencyProperty.Register(nameof(GlyphTypeface), typeof(GlyphTypeface), typeof(DirectText), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateGeometry));
    public static readonly DependencyProperty CodePointProperty = DependencyProperty.Register(nameof(CodePoint), typeof(int), typeof(DirectText), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateGeometry));
    public static readonly DependencyProperty FitToBoundsProperty = DependencyProperty.Register(nameof(FitToBounds), typeof(bool), typeof(DirectText), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public GlyphTypeface? GlyphTypeface { get => (GlyphTypeface?)GetValue(GlyphTypefaceProperty); set => SetValue(GlyphTypefaceProperty, value); }
    public int CodePoint { get => (int)GetValue(CodePointProperty); set => SetValue(CodePointProperty, value); }
    public bool FitToBounds { get => (bool)GetValue(FitToBoundsProperty); set => SetValue(FitToBoundsProperty, value); }
    private Geometry? _outline;
    private (Geometry Geometry, Brush? Brush)[]? _layers;
    private static void InvalidateGeometry(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DirectText)d; control._outline = null; control._layers = null;
    }
    private (Geometry Geometry, Brush? Brush)[] GetLayers()
    {
        if (_layers != null) return _layers;
        if (GlyphTypeface?.CharacterToGlyphMap.TryGetValue(CodePoint, out ushort glyph) != true) return [];
        _layers = ColorGlyphService.ForFace(GlyphTypeface).GetLayers(glyph)?.Select(layer =>
        {
            var geometry = GlyphTypeface.GetGlyphOutline(layer.Glyph, 100, 100);
            if (geometry.CanFreeze) geometry.Freeze();
            return (geometry, layer.Brush);
        }).ToArray() ?? [];
        return _layers;
    }
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
        var layers = GetLayers();
        var geometry = layers.Length == 0 ? GetOutline() : null;
        var bounds = geometry?.Bounds ?? Rect.Empty;
        foreach (var layer in layers) bounds.Union(layer.Geometry.Bounds);
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;
        double availableWidth = Math.Max(1, ActualWidth - Padding.Left - Padding.Right);
        double availableHeight = Math.Max(1, ActualHeight - Padding.Top - Padding.Bottom);
        double scale = Math.Min(availableWidth / bounds.Width, availableHeight / bounds.Height);
        if (!FitToBounds) scale = Math.Min(scale, FontSize / 100);
        dc.PushTransform(new TranslateTransform(Padding.Left + (availableWidth - bounds.Width * scale) / 2 - bounds.X * scale,
            Padding.Top + (availableHeight - bounds.Height * scale) / 2 - bounds.Y * scale));
        dc.PushTransform(new ScaleTransform(scale, scale));
        if (layers.Length == 0) dc.DrawGeometry(Foreground, null, geometry);
        else foreach (var layer in layers) dc.DrawGeometry(layer.Brush ?? Foreground, null, layer.Geometry);
        dc.Pop(); dc.Pop();
    }
}
