using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Helpers;

[Bindable]
public static class CompositionProperty
{
    #region Attached Properties

    #region Scale

    public static Point GetScale(DependencyObject obj)
    {
        return (Point)obj.GetValue(ScaleProperty);
    }

    public static void SetScale(DependencyObject obj, Point value)
    {
        obj.SetValue(ScaleProperty, value);
    }

    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.RegisterAttached("Scale", typeof(Point), typeof(CompositionProperty), new PropertyMetadata(new Point(1, 1), (d, e) =>
        {
            if (d is UIElement u && e.NewValue is Point p)
            {
                u.GetElementVisual().Scale = new(p.ToVector2(), 1);
            }
        }));

    #endregion

    #region Center

    public static Point GetCenter(DependencyObject obj)
    {
        return (Point)obj.GetValue(CenterProperty);
    }

    public static void SetCenter(DependencyObject obj, Point value)
    {
        obj.SetValue(CenterProperty, value);
    }

    public static readonly DependencyProperty CenterProperty =
        DependencyProperty.RegisterAttached("Center", typeof(Point), typeof(CompositionProperty), new PropertyMetadata(new Point(0, 0), (d, e) =>
        {
            if (d is UIElement u && e.NewValue is Point p)
            {
                CompositionFactory.StartCentering(u.GetElementVisual(), (float)p.X, (float)p.Y);
            }
        }));

    #endregion

    #region Translation

    public static Point GetTranslation(DependencyObject obj)
    {
        return (Point)obj.GetValue(TranslationProperty);
    }

    public static void SetTranslation(DependencyObject obj, Point value)
    {
        obj.SetValue(TranslationProperty, value);
    }

    public static readonly DependencyProperty TranslationProperty =
        DependencyProperty.RegisterAttached("Translation", typeof(Point), typeof(CompositionProperty), new PropertyMetadata(new Point(0, 0), (d, e) =>
        {
            if (d is UIElement u && e.NewValue is Point p)
            {
                u.EnableCompositionTranslation()
                 .GetElementVisual()
                 .SetTranslation(new(p.ToVector2(), 0));
            }
        }));

    #endregion

    #endregion
}
