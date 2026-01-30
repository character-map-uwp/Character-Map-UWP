using System.Collections;
using System.Collections.Specialized;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

public interface IExtendedListViewBaseItem
{
    ExtendedListView Owner { get; set; }
}

public class ExtendedListViewItem : ListViewItem, IExtendedListViewBaseItem//, IThemeableControl
{
    //public ThemeHelper _themer;

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
public partial  class ExtendedListView : ListView
{
    long token = 0;

    protected virtual Type GetStyleKey() => typeof(ExtendedListView);

    public ExtendedListView()
    {
        this.DefaultStyleKey = GetStyleKey();
        this.Loaded += ExtendedListView_Loaded;
        this.Unloaded += ExtendedListView_Unloaded;
    }

    private void ExtendedListView_Loaded(object sender, RoutedEventArgs e)
    {
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

        return item;
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
            {
                SelectedItems.Add(item);
            }
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

public class ExtendedGridView : ExtendedListView
{
    protected override Type GetStyleKey() => typeof(ExtendedGridView);

    protected override SelectorItem CreateContainer() => new ExtendedGridViewItem();
}
