using System.Collections;
using System.Collections.Specialized;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Controls;

public class ExtendedListViewItem : ListViewItem, IThemeableControl
{
    public ThemeHelper _themer;
    public ExtendedListViewItem() : base()
    {
        Properties.SetStyleKey(this, "DefaultThemeListViewItemStyle");
        _themer = new ThemeHelper(this);
    }

    public void UpdateTheme()
    {
        _themer.Update();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (this.Style is null)
            _themer.Update();
    }
}

public class ExtendedListView : ListView
{
    #region Dependency Properties

    public INotifyCollectionChanged BindableSelectedItems
    {
        get { return (INotifyCollectionChanged)GetValue(BindableSelectedItemsProperty); }
        set { SetValue(BindableSelectedItemsProperty, value); }
    }

    public static readonly DependencyProperty BindableSelectedItemsProperty =
        DependencyProperty.Register(nameof(BindableSelectedItems), typeof(INotifyCollectionChanged), typeof(ExtendedListView), new PropertyMetadata(null, (d, e) =>
        {
            ((ExtendedListView)d).OnSelectedItemsChanged(e);
        }));

    public bool HasSelection
    {
        get { return (bool)GetValue(HasSelectionProperty); }
        set { SetValue(HasSelectionProperty, value); }
    }

    public static readonly DependencyProperty HasSelectionProperty =
        DependencyProperty.Register(nameof(HasSelection), typeof(bool), typeof(ExtendedListView), new PropertyMetadata(false));

    public int SelectionCount
    {
        get { return (int)GetValue(SelectionCountProperty); }
        set { SetValue(SelectionCountProperty, value); }
    }

    public static readonly DependencyProperty SelectionCountProperty =
        DependencyProperty.Register(nameof(SelectionCount), typeof(int), typeof(ExtendedListView), new PropertyMetadata(0));

    public bool IsSelectionBindingEnabled
    {
        get { return (bool)GetValue(IsSelectionBindingEnabledProperty); }
        set { SetValue(IsSelectionBindingEnabledProperty, value); }
    }

    public static readonly DependencyProperty IsSelectionBindingEnabledProperty =
        DependencyProperty.Register(nameof(IsSelectionBindingEnabled), typeof(bool), typeof(ExtendedListView), new PropertyMetadata(false));

    #endregion




    long token = 0;

    public ExtendedListView()
    {
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

    protected override DependencyObject GetContainerForItemOverride()
    {
        var item = new ExtendedListViewItem { };
        if (ItemContainerStyle != null)
            item.Style = ItemContainerStyle;

        return item;
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

    private void OnSelectedItemsChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged c)
        {
            c.CollectionChanged -= SelectedItems_CollectionChanged;
        }

        if (this is null)
            return;

        if (e.NewValue is INotifyCollectionChanged n)
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
            {
                list.Remove(item);
            }
        }
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
            {
                list.Remove(item);
            }

            foreach (var item in e.AddedItems)
            {
                list.Add(item);
            }

            BindableSelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        UpdateSelection();
    }
}
