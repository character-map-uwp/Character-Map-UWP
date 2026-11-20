using Microsoft.UI.Xaml.Controls;
using System.Collections;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty<Layout>("Layout")]
public partial class LayoutPanel : Panel
{
    private LayoutContext _layoutContext;

    partial void OnLayoutChanged(Layout oldValue, Layout newValue)
    {
        if (oldValue is not null)
        {
            oldValue.MeasureInvalidated -= OnLayoutMeasureInvalidated;
            oldValue.ArrangeInvalidated -= OnLayoutArrangeInvalidated;

            if (_layoutContext is not null)
                oldValue.UninitializeForContext(_layoutContext);
        }

        _layoutContext = null;

        if (newValue is not null)
        {
            _layoutContext = newValue is VirtualizingLayout
                ? new PanelVirtualizingLayoutContext(this)
                : new PanelNonVirtualizingLayoutContext(this);

            newValue.InitializeForContext(_layoutContext);
            newValue.MeasureInvalidated += OnLayoutMeasureInvalidated;
            newValue.ArrangeInvalidated += OnLayoutArrangeInvalidated;
        }

        InvalidateMeasure();
    }

    private void OnLayoutMeasureInvalidated(Layout sender, object args) => InvalidateMeasure();

    private void OnLayoutArrangeInvalidated(Layout sender, object args) => InvalidateArrange();

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Layout is Layout layout && _layoutContext is not null)
            return layout.Measure(_layoutContext, availableSize);

        double maxWidth = 0;
        double maxHeight = 0;

        foreach (UIElement child in Children)
        {
            child.Measure(availableSize);
            if (child.DesiredSize.Width > maxWidth)
                maxWidth = child.DesiredSize.Width;
            if (child.DesiredSize.Height > maxHeight)
                maxHeight = child.DesiredSize.Height;
        }

        return new(maxWidth, maxHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Layout is Layout layout && _layoutContext is not null)
            return layout.Arrange(_layoutContext, finalSize);

        Rect rect = new(0, 0, finalSize.Width, finalSize.Height);
        foreach (UIElement child in Children)
            child.Arrange(rect);

        return finalSize;
    }

    private sealed class PanelNonVirtualizingLayoutContext(LayoutPanel panel) : NonVirtualizingLayoutContext
    {
        private readonly ChildrenReadOnlyList _children = new(panel);
        private object _layoutState;

        protected override object LayoutStateCore
        {
            get => _layoutState;
            set => _layoutState = value;
        }

        protected override IReadOnlyList<UIElement> ChildrenCore => _children;

        private sealed class ChildrenReadOnlyList(LayoutPanel panel) : IReadOnlyList<UIElement>
        {
            private readonly LayoutPanel _panel = panel;

            public UIElement this[int index] => _panel.Children[index];

            public int Count => _panel.Children.Count;

            public IEnumerator<UIElement> GetEnumerator() => ((IEnumerable<UIElement>)_panel.Children).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    private sealed class PanelVirtualizingLayoutContext(LayoutPanel panel) : VirtualizingLayoutContext
    {
        private readonly LayoutPanel _panel = panel;
        private object _layoutState;
        private Point _layoutOrigin;

        protected override object LayoutStateCore
        {
            get => _layoutState;
            set => _layoutState = value;
        }

        protected override int RecommendedAnchorIndexCore => -1;

        protected override Point LayoutOriginCore
        {
            get => _layoutOrigin;
            set => _layoutOrigin = value;
        }

        protected override int ItemCountCore() => _panel.Children.Count;

        protected override object GetItemAtCore(int index) => _panel.Children[index];

        protected override UIElement GetOrCreateElementAtCore(int index, ElementRealizationOptions options) => _panel.Children[index];

        protected override void RecycleElementCore(UIElement element) { }

        protected override Rect RealizationRectCore() => new(0, 0, float.MaxValue, float.MaxValue);
    }
}
