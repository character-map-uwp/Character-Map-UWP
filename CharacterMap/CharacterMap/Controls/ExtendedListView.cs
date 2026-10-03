using Microsoft.Toolkit.Uwp.UI.Controls;
using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Controls;

public interface IExtendedListViewBaseItem
{
    ExtendedListView Owner { get; set; }
}

public class ExtendedListViewItem : ListViewItem, IExtendedListViewBaseItem//, IThemeableControl
{
    //public ThemeHelper _themer;

    public bool IsInRecycleQueue { get; set; }

    public ExtendedListView Owner { get; set; }

    public ExtendedListViewItem() : base()
    {
        Properties.SetStyleKey(this, "DefaultThemeListViewItemStyle");
        _ = new ThemeHelper(this);
    }

    //public void UpdateTheme() =>_themer.Update();

    //protected override void OnApplyTemplate()
    //{
    //    base.OnApplyTemplate();
    //    if (this.Style is null)
    //        _themer.Update();
    //}

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);

        // SuggestionBox requires this for its Footer button
        if (this.Owner is null)
            this.Owner = this.GetFirstAncestorOfType<ExtendedListView>();

        // Support SelectorVisual
        this.Owner?.SetPointerOver(this);
    }
}

public class ExtendedGridViewItem : GridViewItem, IExtendedListViewBaseItem
{
    public ExtendedListView Owner { get; set; }

    public ExtendedGridViewItem() : base()
    {
        Properties.SetStyleKey(this, "DefaultThemeGridViewItemStyle");
        _ = new ThemeHelper(this);
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);

        // Support SelectorVisual
        this.Owner.SetPointerOver(this);
    }
}

[DependencyProperty<bool>("HasSelection")]
[DependencyProperty<bool>("IsSelectionBindingEnabled")]
[DependencyProperty<int>("SelectionCount")]
[DependencyProperty<INotifyCollectionChanged>("BindableSelectedItems")]
[DependencyProperty<DataTemplate>("SelectorTemplate")]
[AttachedProperty<SelectorVisualElement>("SelectorVisual")]
[DependencyProperty<DataTemplate>("ItemToolTipTemplate")]
[DependencyProperty<bool>("EnableRepositionAnimations")]
[DependencyProperty<ICommand>("ItemClickCommand")]
public partial  class ExtendedListView : ListView
{
    long token = 0;

    protected virtual Type GetStyleKey() => typeof(ExtendedListView);

    protected XamlDirect _xamlDirect => field ??= XamlDirect.GetDefault();

    public ExtendedListView()
    {
        this.DefaultStyleKey = GetStyleKey();
        this.Loaded += OnLoaded;
        this.Unloaded += ExtendedListView_Unloaded;

        if (EnableRepositionAnimations)
        {
            this.ContainerContentChanging -= ExtendedListView_ContainerContentChanging;
            this.ContainerContentChanging += ExtendedListView_ContainerContentChanging;
        }

        this.ItemClick += OnItemClick;
    }

    protected virtual void OnLoaded(object sender, RoutedEventArgs e)
    {
        CheckSource(ItemsSource);

        if (IsSelectionBindingEnabled)
            OnAttached();
    }

    private void ExtendedListView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (IsSelectionBindingEnabled)
            OnDetaching();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
    }

    protected override void OnItemsChanged(object e)
    {
        CheckSource(e);
    }

    protected virtual void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (ItemClickCommand is { } cmd
            && cmd.CanExecute(e.ClickedItem))
            cmd.Execute(e.ClickedItem);
    }

    private void CheckSource(object e)
    {
        if (this.IsLoaded is false)
            return;

        e ??= ItemsSource;

        if (e is ISupportIncrementalLoading inc && inc.HasMoreItems && inc is IList list && list.Count == 0)
            _ = inc.LoadMoreItemsAsync((uint)(DataFetchSize <= 0 ? 2 : DataFetchSize));
    }

    protected virtual SelectorItem CreateContainer() => new ExtendedListViewItem();

    protected override DependencyObject GetContainerForItemOverride()
    {
        var item = CreateContainer();

        // Really this should be a Binding but we never change the container
        // style at runtime in the app, so this is more performant.
        if (ItemContainerStyle != null)
            item.Style = ItemContainerStyle;

        // Allows more performant SelectorVisual support
        if (item is IExtendedListViewBaseItem extendedItem)
            extendedItem.Owner = this;

        item.PointerEntered += Item_PointerEntered;

        return item;
    }




    //------------------------------------------------------
    //
    // Reposition Animation Support
    //
    //------------------------------------------------------

    partial void OnEnableRepositionAnimationsChanged(bool o, bool n)
    {
        this.ContainerContentChanging -= ExtendedListView_ContainerContentChanging;
        CompositionFactory.EnableContainerReposition(this, n);

        if (n)
            this.ContainerContentChanging += ExtendedListView_ContainerContentChanging;
    }

    private void ExtendedListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is ExtendedListViewItem i)
            i.IsInRecycleQueue = args.InRecycleQueue;

        if (EnableRepositionAnimations)
        {
            if (args.InRecycleQueue)
            {
                CompositionFactory.PokeUIElementZIndex(args.ItemContainer, _xamlDirect);
            }
            else
            {
                var v = ElementCompositionPreview.GetElementVisual(args.ItemContainer);
                v.ImplicitAnimations = CompositionFactory.GetRepositionCollection(v.Compositor);
            }
        }
    }




    //------------------------------------------------------
    //
    // ToolTip
    //
    //------------------------------------------------------

    private void Item_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (ItemToolTipTemplate is null 
            || sender is not FrameworkElement f 
            || ToolTipService.GetToolTip(f) is ToolTip)
            return;

        ToolTip tooltip = new();
        tooltip.Tag = f;
        tooltip.ContentTemplate = this.ItemToolTipTemplate;
        tooltip.Opened += (s, e) =>
        {
            if (s is ToolTip tt && tt.Tag is FrameworkElement f)
            {
                tt.ContentTemplate = this.ItemToolTipTemplate;
                tt.Content = f.Tag;
            }
        };
        
        if (ResourceHelper.TryGet("DefaultThemeToolTipStyle", out Style style))
            tooltip.Style = style;

        ToolTipService.SetToolTip(f, tooltip);
    }




    //------------------------------------------------------
    //
    // Selection Binding Support
    //
    //------------------------------------------------------

    partial void OnBindableSelectedItemsChanged(INotifyCollectionChanged o, INotifyCollectionChanged n)
    {
        if (o is not null)
        {
            o.CollectionChanged -= SelectedItems_CollectionChanged;
        }

        if (this is null)
            return;

        if (n is not null)
        {
            n.CollectionChanged -= SelectedItems_CollectionChanged;
            n.CollectionChanged += SelectedItems_CollectionChanged;
        }
    }

    void ItemsSourceChanged(DependencyObject source, DependencyProperty property)
    {
        if (SelectedItems is IList<object> list)
        {
            foreach (var item in list.ToList())
                list.Remove(item);
        }
    }

    void UpdateSelection()
    {
        HasSelection = SelectedItems.Count > 0;
        SelectionCount = SelectedItems.Count;
    }

    public void ClearSelection()
    {
        SelectedItems.Clear();
    }

    protected void OnAttached()
    {
        if (BindableSelectedItems == null)
        {
            BindableSelectedItems = new ObservableCollection<object>();
        }
        else if (BindableSelectedItems is IEnumerable<object> list)
        {
            foreach (var item in list.ToList())
                SelectedItems.Add(item);
        }

        token = RegisterPropertyChangedCallback(ListViewBase.ItemsSourceProperty, ItemsSourceChanged);

        SelectionChanged -= AssociatedObject_SelectionChanged;
        SelectionChanged += AssociatedObject_SelectionChanged;

        BindableSelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
        BindableSelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

        UpdateSelection();
    }


    protected void OnDetaching()
    {
        UnregisterPropertyChangedCallback(ListViewBase.ItemsSourceProperty, token);
        SelectionChanged -= AssociatedObject_SelectionChanged;

        if (BindableSelectedItems is not null)
            BindableSelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
    }

    private void SelectedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        SelectionChanged -= AssociatedObject_SelectionChanged;

        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var item in e.NewItems)
            {
                if (!SelectedItems.Contains(item))
                    SelectedItems.Add(item);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            foreach (var item in e.OldItems)
            {
                if (!SelectedItems.Contains(item))
                    SelectedItems.Remove(item);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DeselectRange(new ItemIndexRange(0, int.MaxValue));
        }

        UpdateSelection();
        SelectionChanged += AssociatedObject_SelectionChanged;
    }

    private void AssociatedObject_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BindableSelectedItems is IList list)
        {
            BindableSelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;

            foreach (var item in e.RemovedItems)
                list.Remove(item);

            foreach (var item in e.AddedItems)
                list.Add(item);

            BindableSelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        UpdateSelection();
    }




    //------------------------------------------------------
    //
    // Selector Visual Support
    //
    //------------------------------------------------------

    partial void OnSelectorTemplateChanged(DataTemplate o, DataTemplate n)
    {
        if (o is not null && n is null)
            SetSelectorVisual(this, null);
        else if (n is not null && n.LoadContent() is SelectorVisualElement vis)
            SetSelectorVisual(this, vis);
    }

    public void RegisterHeader(ListViewBaseHeaderItem item)
    {
        item.PointerEntered -= HeaderItem_PointerEntered;
        item.PointerEntered += HeaderItem_PointerEntered;
    }

    private void HeaderItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
            SetPointerOver(element);
    }

    internal void SetPointerOver(FrameworkElement element)
    {
        if (GetSelectorVisual(this) is { } vis)
            vis?.MoveTo(element, VisualTreeHelper.GetParent(vis) as FrameworkElement, true);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        if (GetSelectorVisual(this) is { } vis)
            vis?.Hide();
    }
}




//------------------------------------------------------
//
// ExtendedGridView
//
//------------------------------------------------------

[DependencyProperty<bool>("StretchContainersForSingleRowEnabled")] // When in adaptive mode, whether to stretch containers to fit if there is only a single row of content
[DependencyProperty<double>("DesiredItemWidth")] // Enables adaptive layout mode
[DependencyProperty<double>("RenderedItemWidth")] 
public partial class ExtendedGridView : ExtendedListView
{
    protected override Type GetStyleKey() => typeof(ExtendedGridView);

    protected override SelectorItem CreateContainer() => new ExtendedGridViewItem();




    //------------------------------------------------------
    //
    // Adaptive Mode Support
    //
    //------------------------------------------------------

    protected override DependencyObject GetContainerForItemOverride()
    {
        FrameworkElement f = (FrameworkElement)base.GetContainerForItemOverride();

        if (_isAdaptive && this.ItemsPanelRoot is not ItemsWrapGrid)
            f.Width = RenderedItemWidth;

        return f;
    }

    bool _isAdaptive;

    Storyboard _disableLayoutRoundingStoryboard => field ??= (new Storyboard()).With(sb =>
    {
        // We use a storyboard so we can retain the original DependencyProperty value
        sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(this, nameof(this.UseLayoutRounding))
           .AddKeyFrame(0, false);
    });


    partial void OnDesiredItemWidthChanged(double o, double n)
    {
        Items.VectorChanged -= ItemsOnVectorChanged;
        this.SizeChanged -= OnSizeChanged;

        _isAdaptive = !(double.IsNaN(DesiredItemWidth) || double.IsInfinity(DesiredItemWidth) || DesiredItemWidth == 0);

        if (!_isAdaptive)
        {
            // Disable our UseLayoutRounding override and return to the user-chosen value
            _disableLayoutRoundingStoryboard.Stop();

            if (this.ItemsPanelRoot is { } root)
                if (root is ItemsWrapGrid g)
                    g.ClearValue(ItemsWrapGrid.ItemWidthProperty);
                else
                    foreach (var container in root.Children)
                        container.ClearValue(FrameworkElement.WidthProperty);
        }
        else
        {
            // We will need to disable layout rounding to prevent crashing
            _disableLayoutRoundingStoryboard.Begin();
            RecalculateLayout(ActualWidth);

            Items.VectorChanged += ItemsOnVectorChanged;
            SizeChanged += OnSizeChanged;
        }
    }

    protected virtual void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // If we are in center alignment, we only care about relayout if the number of columns we can display changes
        // Fixes #1737
        if (HorizontalAlignment != HorizontalAlignment.Stretch)
        {
            var prevColumns = CalculateColumns(e.PreviousSize.Width, DesiredItemWidth);
            var newColumns = CalculateColumns(e.NewSize.Width, DesiredItemWidth);

            // If the width of the internal list view changes, check if more or less columns needs to be rendered.
            if (prevColumns != newColumns)
                RecalculateLayout(e.NewSize.Width);
        }
        else if (e.PreviousSize.Width != e.NewSize.Width)
        {
            // We need to recalculate width as our size changes to adjust internal items.
            RecalculateLayout(e.NewSize.Width);
        }
    }

    private void ItemsOnVectorChanged(IObservableVector<object> sender, IVectorChangedEventArgs @event)
    {
        RecalculateLayout(ActualWidth);
    }

    bool _needContainerMarginForLayout = false;

    private void RecalculateLayout(double containerWidth)
    {
        if (double.IsNaN(containerWidth) || containerWidth == 0 || double.IsInfinity(containerWidth))
            return;

        var itemsPanel = ItemsPanelRoot as Panel;
        var panelMargin = itemsPanel != null ?
                          itemsPanel.Margin.Left + itemsPanel.Margin.Right :
                          0;
        var padding = Padding.Left + Padding.Right;
        var border = BorderThickness.Left + BorderThickness.Right;

        // width should be the displayable width
        containerWidth = containerWidth - padding - panelMargin - border;
        if (containerWidth > 0)
        {
            var newWidth = CalculateItemWidth(containerWidth);
            RenderedItemWidth = Math.Floor(newWidth);
            UpdateWidths();
        }
    }

    Thickness _itemMargin = default;

    /// <summary>
    /// Calculates the width of the grid items.
    /// </summary>
    /// <param name="containerWidth">The width of the container control.</param>
    /// <returns>The calculated item width.</returns>
    protected virtual double CalculateItemWidth(double containerWidth)
    {
        if (double.IsNaN(DesiredItemWidth) && DesiredItemWidth > 0)
            return DesiredItemWidth;

        var columns = CalculateColumns(containerWidth, DesiredItemWidth);

        // If we want to stretch containers and there are less items than there are columns, reduce the column count
        if (Items != null && Items.Count > 0 && Items.Count < columns && StretchContainersForSingleRowEnabled)
            columns = Items.Count;

        // Subtract the margin from the width so we place the correct width for placement
        var fallbackThickness = default(Thickness);
        var itemMargin = _itemMargin = AdaptiveHeightValueConverter.GetItemMargin(this, fallbackThickness);
        
        // No style explicitly defined, or no items or no container for the items
        // We need to get an actual margin for proper layout
        _needContainerMarginForLayout = itemMargin == fallbackThickness;

        return (containerWidth / columns) - itemMargin.Left - itemMargin.Right;
    }

    private static int CalculateColumns(double containerWidth, double itemWidth)
        => Math.Max(1, (int)Math.Floor(containerWidth / itemWidth));

    void UpdateWidths()
    {
        if (this.ItemsPanelRoot is null)
            return;

        var w = RenderedItemWidth;
        if (this.ItemsPanelRoot is ItemsWrapGrid g)
            g.ItemWidth = w + _itemMargin.Left + _itemMargin.Right;
        else
            foreach (var item in this.ItemsPanelRoot.Children.OfType<SelectorItem>())
                _xamlDirect.SetDoubleProperty(_xamlDirect.GetXamlDirectObject(item), XamlPropertyIndex.FrameworkElement_Width, w);
    }
}
