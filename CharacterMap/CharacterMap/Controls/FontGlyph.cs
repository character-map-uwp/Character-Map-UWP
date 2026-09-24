using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

/// <summary>
/// A glyph contained inside a Composition ViewBox that has an option of scaling using the compositor instead
/// </summary>
[DependencyProperty<Stretch>("Stretch", Stretch.Uniform, "InvalidateMeasure")]
[DependencyProperty<StretchDirection>("StretchDirection", Windows.UI.Xaml.Controls.StretchDirection.DownOnly, "InvalidateMeasure")]
[DependencyProperty<DWriteFontFace>("FontFace", null, "InvalidateMeasure")]
[DependencyProperty<Uri>("FontUri")]
[DependencyProperty<string>("Indices")]
[DependencyProperty<StyleSimulations>]
[DependencyProperty<bool>("IsColorFontEnabled")]
public sealed partial class FontGlyph : Control
{
    const double REAL_EPSILON = 1.192092896e-07d;  /* FLT_EPSILON */

    Glyphs _presenter;

    Size _scale = new(1, 1);

    public FontGlyph()
    {
        this.DefaultStyleKey = typeof(FontGlyph);
        //this.DataContextChanged += (s, e) => InvalidateMeasure();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _presenter = this.GetTemplateChild("Presenter") as Glyphs;
    }

    //protected override void OnContentChanged(object oldContent, object newContent)
    //{
    //    base.OnContentChanged(oldContent, newContent);
    //    InvalidateMeasure();
    //}

    public Size GetCurrentScaleFactor() => _scale;




    //-------------------------------------------------------------------------
    //
    //   Return TRUE if two points are close. Close is defined as near enough
    //   that the rounding to 32bit float precision could have resulted in the
    //   difference. We define an arbitrary number of allowed rounding errors (10).
    //   We divide by b to normalize the difference. It doesn't matter which point
    //   we divide by - if they're significantly different, we'll return true, and
    //   if they're really close, then a==b (almost).
    //
    // Arguments:
    //
    //   a, b - input numbers to compare.
    //
    // Return Value:
    //
    //   TRUE if the numbers are close enough.
    //
    //-------------------------------------------------------------------------
    bool IsCloseReal(double a, double b)
    {
        // if b == 0.0f we don't want to divide by zero. If this happens
        // it's sufficient to use 1.0 as the divisor because REAL_EPSILON
        // should be good enough to test if a number is close enough to zero.

        // NOTE: if b << a, this could cause an FP overflow. Currently we mask
        // these exceptions, but if we unmask them, we should probably check
        // the divide.

        // We assume we can generate an overflow exception without taking down
        // the system. We will still get the right results based on the FPU
        // default handling of the overflow.

        // Ensure that anyone clearing the overflow mask comes and revisits this
        // assumption. If you hit this Assert, it means that the #O exception mask
        // has been cleared. Go check c_wFPCtrlExceptions.

        return (Math.Abs((a - b) / ((b == 0.0d) ? 1.0d : b)) < 10.0d * REAL_EPSILON);
    }




    //-------------------------------------------------------------------------
    //
    //  Function:   ViewContainer::ComputeScaleFactor()
    //
    //  Synopsis:   Compute the scale factor of the Child content.
    //
    //-------------------------------------------------------------------------
    Size ComputeScaleFactor(Size availableSize, Size contentSize)
    {
        Size desiredSize;
        double scaleX = 1.0;
        double scaleY = 1.0;

        bool isConstrainedWidth = !double.IsInfinity(availableSize.Width);
        bool isConstrainedHeight = !double.IsInfinity(availableSize.Height);

        // Don't scale if we shouldn't stretch or the scaleX and scaleY are both infinity.
        if (Stretch != Stretch.None && (isConstrainedWidth || isConstrainedHeight))
        {
            bool isZeroWidth = IsCloseReal(contentSize.Width, 0.0) || contentSize.Width <= 0.0;
            bool isZeroHeight = IsCloseReal(contentSize.Height, 0.0) || contentSize.Height <= 0.0;

            // If content has no size at all (e.g. unmeasured or empty), do not scale
            if (isZeroWidth && isZeroHeight)
            {
                desiredSize.Width = 1.0;
                desiredSize.Height = 1.0;
                return desiredSize;
            }

            // Compute the individual scaleX and scaleY scale factors.
            // If one dimension is 0 (e.g. zero-advance combining marks or zero-height rules),
            // do not divide by zero or collapse scale to 0.
            scaleX = isZeroWidth ? 1.0 : (availableSize.Width / contentSize.Width);
            scaleY = isZeroHeight ? 1.0 : (availableSize.Height / contentSize.Height);

            if (isZeroWidth)
                scaleX = scaleY;
            else if (isZeroHeight)
                scaleY = scaleX;

            // Make the scale factors uniform by setting them both equal to
            // the larger or smaller (depending on infinite lengths and the
            // Stretch value)
            if (!isConstrainedWidth)
                scaleX = scaleY;
            else if (!isConstrainedHeight)
                scaleY = scaleX;
            else
            {
                switch (Stretch)
                {
                    case Stretch.Uniform:
                        // Use the smaller factor for both
                        scaleX = scaleY = Math.Min(scaleX, scaleY);
                        break;
                    case Stretch.UniformToFill:
                        // Use the larger factor for both
                        scaleX = scaleY = Math.Max(scaleX, scaleY);
                        break;
                    case Stretch.Fill:
                    default:
                        break;
                }
            }

            // Prevent scaling in an undesired direction
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
                default:
                    break;
            }
        }

        desiredSize.Width = scaleX;
        desiredSize.Height = scaleY;

        return desiredSize;
    }




    //------------------------------------------------------------------------
    //
    //  Method:   ViewContainer::MeasureOverride
    //
    //  Synopsis: Returns the desired size for layout purposes.
    //
    //------------------------------------------------------------------------
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

        Size desiredSize = new(
            _scale.Width * childDesiredSize.Width,
            _scale.Height * childDesiredSize.Height);

        return desiredSize;
    }




    //------------------------------------------------------------------------
    //
    //  Method:   ViewContainer::ArrangeOverride
    //
    //  Synopsis: Returns the final render size for layout purposes.
    //
    //------------------------------------------------------------------------
    //protected override Size ArrangeOverride(Size finalSize)
    //{
    //    if (_presenter is null)
    //        ApplyTemplate();

    //    if (_presenter is null)
    //        return finalSize;

    //    Size desiredSize = _presenter.DesiredSize;
    //    _scale = ComputeScaleFactor(finalSize, desiredSize);

    //    // Position the Child centered within the Viewbox
    //    Rect originalPosition = new(
    //        (finalSize.Width - desiredSize.Width) / 2.0,
    //        (finalSize.Height - desiredSize.Height) / 2.0,
    //        desiredSize.Width,
    //        desiredSize.Height);

    //    _presenter.Arrange(originalPosition);

    //    // Scale the ChildElement by the necessary factor around its center
    //    Visual v = _presenter.GetElementVisual();
    //    //_presenter.RenderTransform = null;
    //    //CompositionFactory.StartCentering(v);
    //    v.CenterPoint = new((float)desiredSize.Width / 2f, (float)desiredSize.Height / 2f, 0);
    //    v.Scale = new(_scale.ToVector2(), 1);

    //    return finalSize;
    //}

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
        if (FontFace is { } face && ushort.TryParse(Indices, out ushort glyphId))
        {
            Rect bounds = face.GetDesignGlyphBounds(glyphId);
            if (bounds.Width > 0 && face.DesignUnitsPerEm > 0)
            {
                double emScale = FontSize / face.DesignUnitsPerEm;
                double inkLeft = bounds.X * emScale;
                double inkWidth = bounds.Width * emScale;
                // Center the ink bounds within finalSize.Width:
                originX = (finalSize.Width - inkWidth) / 2.0 - inkLeft;
            }
        }


        Rect originalPosition = new(
            originX,
            (finalSize.Height - desiredSize.Height) / 2.0,
            desiredSize.Width,
            desiredSize.Height);

        _presenter.Arrange(originalPosition);

        Visual v = _presenter.GetElementVisual();
        v.CenterPoint = new((float)(finalSize.Width / 2.0 - originX), (float)desiredSize.Height / 2f, 0);
        v.Scale = new(_scale.ToVector2(), 1);

        return finalSize;
    }

}