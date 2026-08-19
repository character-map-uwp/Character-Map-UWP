using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty<Orientation>("Orientation", Orientation.Vertical, nameof(OnLayoutPropertyChanged))]
[DependencyProperty<double>("Spacing", 0d, nameof(OnLayoutPropertyChanged))]
[DependencyProperty<Thickness>("Padding", "new Thickness(0d)", nameof(OnLayoutPropertyChanged))]
[AttachedProperty<bool>("ExcludeFromMeasure", false)]
public partial class StackPanelEx : Panel
{
    private void OnLayoutPropertyChanged()
    {
        InvalidateMeasure();
        InvalidateArrange();
    }

    static partial void OnExcludeFromMeasureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element && VisualTreeHelper.GetParent(element) is StackPanelEx panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Thickness padding = Padding;
        double spacing = Spacing;
        Orientation orientation = Orientation;

        Size childAvailableSize = new(
            Math.Max(0, availableSize.Width - padding.Left - padding.Right),
            Math.Max(0, availableSize.Height - padding.Top - padding.Bottom));

        if (orientation == Orientation.Vertical)
            childAvailableSize.Height = double.PositiveInfinity;
        else
            childAvailableSize.Width = double.PositiveInfinity;

        double totalWidth = 0;
        double totalHeight = 0;
        bool hasMeasuredChild = false;

        foreach (UIElement child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            child.Measure(childAvailableSize);

            if (GetExcludeFromMeasure(child))
                continue;

            Size desired = child.DesiredSize;

            if (orientation == Orientation.Vertical)
            {
                if (hasMeasuredChild)
                    totalHeight += spacing;

                totalHeight += desired.Height;
                totalWidth = Math.Max(totalWidth, desired.Width);
            }
            else
            {
                if (hasMeasuredChild)
                    totalWidth += spacing;

                totalWidth += desired.Width;
                totalHeight = Math.Max(totalHeight, desired.Height);
            }

            hasMeasuredChild = true;
        }

        return new(
            totalWidth + padding.Left + padding.Right,
            totalHeight + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Thickness padding = Padding;
        double spacing = Spacing;
        Orientation orientation = Orientation;

        double x = padding.Left;
        double y = padding.Top;

        double availableWidth = Math.Max(0, finalSize.Width - padding.Left - padding.Right);
        double availableHeight = Math.Max(0, finalSize.Height - padding.Top - padding.Bottom);

        bool isFirst = true;

        foreach (UIElement child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            if (!isFirst)
            {
                if (orientation == Orientation.Vertical)
                    y += spacing;
                else
                    x += spacing;
            }

            Size desired = child.DesiredSize;

            if (orientation == Orientation.Vertical)
            {
                double childWidth = Math.Max(desired.Width, availableWidth);
                child.Arrange(new Rect(x, y, childWidth, desired.Height));
                y += desired.Height;
            }
            else
            {
                double childHeight = Math.Max(desired.Height, availableHeight);
                child.Arrange(new Rect(x, y, desired.Width, childHeight));
                x += desired.Width;
            }

            isFirst = false;
        }

        return finalSize;
    }
}
