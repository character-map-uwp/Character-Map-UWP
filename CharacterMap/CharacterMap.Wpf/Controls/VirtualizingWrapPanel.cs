using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CharacterMap.Wpf.Controls;

/// <summary>Pixel-scrolling, viewport-only realization for large Unicode character maps.</summary>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(nameof(ItemSize), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(88d, FrameworkPropertyMetadataOptions.AffectsMeasure), value => (double)value >= 32 && double.IsFinite((double)value));
    public double ItemSize { get => (double)GetValue(ItemSizeProperty); set => SetValue(ItemSizeProperty, value); }
    private Size _extent, _viewport;
    private double _offset;
    public int Columns { get; private set; } = 1;
    private double _cellWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner == null) return default;
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : ItemSize;
        double height = double.IsFinite(availableSize.Height) ? availableSize.Height : ItemSize * 6;
        Columns = Math.Max(1, (int)(width / ItemSize));
        _cellWidth = width / Columns;
        _viewport = new Size(width, height);
        _extent = new Size(width, Math.Ceiling(owner.Items.Count / (double)Columns) * ItemSize);
        _offset = Math.Clamp(_offset, 0, Math.Max(0, _extent.Height - height));
        ScrollOwner?.InvalidateScrollInfo();
        int first = (int)(_offset / ItemSize) * Columns;
        int last = Math.Min(owner.Items.Count - 1, ((int)((_offset + height) / ItemSize) + 1) * Columns - 1);
        // Accessing InternalChildren connects the panel to the owner's generator on first layout.
        var children = InternalChildren;
        var generator = ItemContainerGenerator;
        // Remove offscreen containers before generating new ones. Never materialize the full font.
        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            int index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index < first || index > last)
            {
                generator.Remove(new GeneratorPosition(i, 0), 1);
                RemoveInternalChildRange(i, 1);
            }
        }
        var start = generator.GeneratorPositionFromIndex(first);
        int childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
        using (generator.StartAt(start, GeneratorDirection.Forward, true))
        {
            for (int index = first; index <= last; index++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out bool created);
                if (created)
                {
                    if (childIndex >= InternalChildren.Count) AddInternalChild(child);
                    else InsertInternalChild(childIndex, child);
                    generator.PrepareItemContainer(child);
                }
                child.Measure(new Size(_cellWidth, ItemSize));
            }
        }
        return _viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            int index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            InternalChildren[i].Arrange(new Rect(index % Columns * _cellWidth, index / Columns * ItemSize - _offset, _cellWidth, ItemSize));
        }
        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        // A reset invalidates generator positions; remove the old visual containers as well.
        if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) RemoveInternalChildRange(0, InternalChildren.Count);
        else if (args.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove or System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
        InvalidateMeasure();
    }

    public void ScrollToIndex(int index)
    {
        double top = index / Columns * ItemSize;
        if (top < _offset) SetVerticalOffset(top);
        else if (top + ItemSize > _offset + _viewport.Height) SetVerticalOffset(top + ItemSize - _viewport.Height);
    }
    protected override void BringIndexIntoView(int index) => ScrollToIndex(index);
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        DependencyObject current = visual;
        while (VisualTreeHelper.GetParent(current) is { } parent && parent != this) current = parent;
        if (current is UIElement element)
        {
            int child = InternalChildren.IndexOf(element);
            if (child >= 0) ScrollToIndex(ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(child, 0)));
        }
        return rectangle;
    }
    public void SetVerticalOffset(double offset)
    {
        if (double.IsNaN(offset)) return;
        _offset = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        InvalidateMeasure();
        ScrollOwner?.InvalidateScrollInfo();
    }
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => 0;
    public double VerticalOffset => _offset;
    public ScrollViewer? ScrollOwner { get; set; }
    public void LineUp() => SetVerticalOffset(_offset - ItemSize);
    public void LineDown() => SetVerticalOffset(_offset + ItemSize);
    public void MouseWheelUp() => SetVerticalOffset(_offset - ItemSize * 3);
    public void MouseWheelDown() => SetVerticalOffset(_offset + ItemSize * 3);
    public void PageUp() => SetVerticalOffset(_offset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(_offset + ViewportHeight);
    public void LineLeft() { }
    public void LineRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void PageLeft() { }
    public void PageRight() { }
    public void SetHorizontalOffset(double offset) { }
}
