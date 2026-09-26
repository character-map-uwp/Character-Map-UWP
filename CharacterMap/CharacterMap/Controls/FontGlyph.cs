using CharacterMap.Core;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

/// <summary>
/// A glyph contained inside a Composition ViewBox that has an option of scaling using the compositor instead
/// </summary>
[DependencyProperty<Stretch>("Stretch", Stretch.Uniform, "InvalidateMeasure")]
[DependencyProperty<StretchDirection>("StretchDirection", Windows.UI.Xaml.Controls.StretchDirection.DownOnly, "InvalidateMeasure")]
[DependencyProperty<DWriteFontFace>("FontFace")]
[DependencyProperty<Uri>("FontUri")]
[DependencyProperty<string>("Indices")]
[DependencyProperty<StyleSimulations>]
[DependencyProperty<bool>("IsColorFontEnabled")]
public sealed partial class FontGlyph : Control
{
    const double REAL_EPSILON = 1.192092896e-07d;  /* FLT_EPSILON */

    Glyphs _presenter;
    Visual _visual;
    Size _scale = new(1, 1);

    bool _hasInkBounds;
    double _designInkLeft;
    double _designInkWidth;
    double _designUnitsPerEm;

    public FontGlyph()
    {
        DefaultStyleKey = typeof(FontGlyph);
        RegisterPropertyChangedCallback(FontSizeProperty, (d, e) => ((FontGlyph)d).OnFontSizePropertyChanged());
        RegisterPropertyChangedCallback(ForegroundProperty, (d, e) => ((FontGlyph)d).OnForegroundPropertyChanged());
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _presenter = GetTemplateChild("Presenter") as Glyphs;
        if (_presenter is not null)
        {
            _visual = _presenter.GetElementVisual();
            UpdatePresenterAll();
        }
    }

    void UpdatePresenterAll()
    {
        if (_presenter is null)
            return;

        Properties.SetFontUri(_presenter, FontUri);
        _presenter.Fill = Foreground;
        _presenter.FontRenderingEmSize = FontSize;
        _presenter.Indices = Indices;
        _presenter.IsColorFontEnabled = IsColorFontEnabled;
        _presenter.StyleSimulations = StyleSimulations;
    }

    partial void OnFontFaceChanged(DWriteFontFace o, DWriteFontFace n)
    {
        UpdateInkMetrics();
        InvalidateMeasure();
    }

    partial void OnFontUriChanged(Uri o, Uri n)
    {
        if (_presenter is not null)
            Properties.SetFontUri(_presenter, n);
    }

    partial void OnIndicesChanged(string o, string n)
    {
        _presenter?.Indices = n;
        UpdateInkMetrics();
        InvalidateMeasure();
    }

    partial void OnIsColorFontEnabledChanged(bool o, bool n)
    {
        _presenter?.IsColorFontEnabled = n;
    }

    partial void OnStyleSimulationsChanged(StyleSimulations o, StyleSimulations n)
    {
        _presenter?.StyleSimulations = n;
    }

    void OnFontSizePropertyChanged()
    {
        _presenter?.FontRenderingEmSize = FontSize;
        InvalidateMeasure();
    }

    void OnForegroundPropertyChanged()
    {
        _presenter?.Fill = Foreground;
    }

    void UpdateInkMetrics()
    {
        _hasInkBounds = false;
        if (FontFace is { } face && ushort.TryParse(Indices, out ushort glyphId) && face.DesignUnitsPerEm > 0)
        {
            Rect bounds = face.GetDesignGlyphBounds(glyphId);
            if (bounds.Width > 0)
            {
                _hasInkBounds = true;
                _designInkLeft = bounds.X;
                _designInkWidth = bounds.Width;
                _designUnitsPerEm = face.DesignUnitsPerEm;
            }
        }
    }

    public Size GetCurrentScaleFactor() => _scale;

    bool IsCloseReal(double a, double b)
    {
        return Math.Abs((a - b) / ((b == 0.0d) ? 1.0d : b)) < 10.0d * REAL_EPSILON;
    }

    Size ComputeScaleFactor(Size availableSize, Size contentSize)
    {
        if (Stretch == Stretch.None)
            return new(1, 1);

        bool isConstrainedWidth = !double.IsInfinity(availableSize.Width);
        bool isConstrainedHeight = !double.IsInfinity(availableSize.Height);

        if (!isConstrainedWidth && !isConstrainedHeight)
            return new(1, 1);

        bool isZeroWidth = IsCloseReal(contentSize.Width, 0.0) || contentSize.Width <= 0.0;
        bool isZeroHeight = IsCloseReal(contentSize.Height, 0.0) || contentSize.Height <= 0.0;

        if (isZeroWidth && isZeroHeight)
            return new(1, 1);

        // Fast-path: if DownOnly and content fits within available size, no scaling needed
        if (StretchDirection == StretchDirection.DownOnly
            && (isZeroWidth || !isConstrainedWidth || contentSize.Width <= availableSize.Width)
            && (isZeroHeight || !isConstrainedHeight || contentSize.Height <= availableSize.Height))
            return new(1, 1);

        double scaleX = isZeroWidth ? 1.0 : (availableSize.Width / contentSize.Width);
        double scaleY = isZeroHeight ? 1.0 : (availableSize.Height / contentSize.Height);

        if (isZeroWidth)
            scaleX = scaleY;
        else if (isZeroHeight)
            scaleY = scaleX;

        if (!isConstrainedWidth)
            scaleX = scaleY;
        else if (!isConstrainedHeight)
            scaleY = scaleX;
        else
        {
            switch (Stretch)
            {
                case Stretch.Uniform:
                    scaleX = scaleY = Math.Min(scaleX, scaleY);
                    break;
                case Stretch.UniformToFill:
                    scaleX = scaleY = Math.Max(scaleX, scaleY);
                    break;
                case Stretch.Fill:
                default:
                    break;
            }
        }

        switch (StretchDirection)
        {
            case Windows.UI.Xaml.Controls.StretchDirection.UpOnly:
                scaleX = Math.Max(1.0, scaleX);
                scaleY = Math.Max(1.0, scaleY);
                break;
            case Windows.UI.Xaml.Controls.StretchDirection.DownOnly:
                scaleX = Math.Min(1.0, scaleX);
                scaleY = Math.Min(1.0, scaleY);
                break;
        }

        return new(scaleX, scaleY);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size childDesiredSize = default;
        Size infiniteSize = new(double.PositiveInfinity, double.PositiveInfinity);

        if (_presenter is null)
            ApplyTemplate();

        if (_presenter is FrameworkElement f)
        {
            f.Measure(infiniteSize);
            childDesiredSize = f.DesiredSize;
        }

        Size newScale = ComputeScaleFactor(availableSize, childDesiredSize);
        if (!IsCloseReal(newScale.Width, _scale.Width) || !IsCloseReal(newScale.Height, _scale.Height))
        {
            _scale = newScale;
            InvalidateArrange();
        }

        return new(
            _scale.Width * childDesiredSize.Width,
            _scale.Height * childDesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_presenter is null)
            ApplyTemplate();

        if (_presenter is null)
            return finalSize;

        Size desiredSize = _presenter.DesiredSize;
        _scale = ComputeScaleFactor(finalSize, desiredSize);

        // If width is 0 (combining mark / subjoined ligature), the ink extends ~1.35em
        // to the left of origin. Shift the origin to the right so the ink centers in the box.
        double originX = (finalSize.Width - desiredSize.Width) / 2.0;
        if (_hasInkBounds)
        {
            double emScale = FontSize / _designUnitsPerEm;
            double inkLeft = _designInkLeft * emScale;
            double inkWidth = _designInkWidth * emScale;
            // Center the ink bounds within finalSize.Width:
            originX = (finalSize.Width - inkWidth) / 2.0 - inkLeft;
        }

        Rect originalPosition = new(
            originX,
            (finalSize.Height - desiredSize.Height) / 2.0,
            desiredSize.Width,
            desiredSize.Height);

        _presenter.Arrange(originalPosition);

        if (_visual is not null)
        {
            Vector3 centerPoint = new((float)(finalSize.Width / 2.0 - originX), (float)desiredSize.Height / 2f, 0);
            Vector3 scale = new(_scale.ToVector2(), 1);

            if (_visual.CenterPoint != centerPoint)
                _visual.CenterPoint = centerPoint;

            if (_visual.Scale != scale)
                _visual.Scale = scale;
        }

        return finalSize;
    }
}