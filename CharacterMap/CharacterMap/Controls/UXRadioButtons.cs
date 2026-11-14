using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using System.Collections;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

public class UXRadioButtonsElementPreparedEventArgs : EventArgs
{
    public UIElement Element { get; }
    public int Index { get; }

    public UXRadioButtonsElementPreparedEventArgs(UIElement element, int index)
    {
        Element = element;
        Index = index;
    }
}

/// <summary>
/// Extends Selector with native selector visual support and additional useful events
/// </summary>
[AttachedProperty<double>("ColumnSpacing", 0d)]
[AttachedProperty<double>("RowSpacing", 0d)]
[AttachedProperty<DataTemplate>("LayoutTemplate")]
[DependencyProperty<bool>("ForceSelection")] // Attempt to persist the selected index between ItemsSource changes
[DependencyProperty<ItemsSelectionModel>("SelectedItemsSource")]
[DependencyProperty<int>("MaxColumns", 0, nameof(OnSpacingPropertyChanged))]
[DependencyProperty<Orientation>("Orientation", "Orientation.Horizontal", nameof(OnSpacingPropertyChanged))]
public partial class UXRadioButtons : ListBox
{
    public double ColumnSpacing
    {
        get => GetColumnSpacing(this);
        set => SetColumnSpacing(this, value);
    }

    public double RowSpacing
    {
        get => GetRowSpacing(this);
        set => SetRowSpacing(this, value);
    }

    public ICommand SelectedIndexChangedCommand { get; set; }

    public event TypedEventHandler<UXRadioButtons, UXRadioButtonsElementPreparedEventArgs> ElementPrepared;

    private Vector2 _prevPoint = Vector2.Zero;
    private bool _isUpdatingSelection;
    private long _itemsSourceToken = 0;

    private Debouncer _debouncer => field ?? new(8);

    public UXRadioButtons()
    {
        this.DefaultStyleKey = typeof(UXRadioButtons);
        this.Loaded += UXRadioButtons_Loaded;
        this.Unloaded += UXRadioButtons_Unloaded;
        this.SelectionChanged += UXRadioButtons_SelectionChanged;
    }

    partial void OnSelectedItemsSourceChanged(ItemsSelectionModel o, ItemsSelectionModel n)
    {
        this.ItemsSource = n?.ItemsSource;
        this.SelectedItem = n?.SelectedItem;
    }

    private void ItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (ItemsSource is null || !this.ForceSelection)
            return;

        _debouncer.Debounce(() =>
        {
            if (this.SelectedItem is null && this.ItemsSource is IList { Count: > 0 } list)
                this.SelectedItem = list[0];

            if (SelectorVisualElement.GetElement(this) is not { } selector)
                return;

            selector.MoveTo(
                (this.ContainerFromItem(this.SelectedItem) ?? this.GetFirstLevelDescendantsOfType<RadioButton>().FirstOrDefault(f => f.DataContext == this.SelectedItem || f == this.SelectedItem)) as FrameworkElement,
                this,
                animate: false);
        });
    }

    private void UXRadioButtons_Loaded(object sender, RoutedEventArgs e)
    {
        if (_itemsSourceToken == 0)
            _itemsSourceToken = this.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ItemsSourceChanged);

        UpdateContainersSelection();

        if (Properties.GetZoomHelper(this) is { } zoomHelper)
            zoomHelper.Attach(this);

        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        selector.Target = null;
        selector.Target = this;

        FrameworkElement target = null;
        
        if (this.SelectedIndex >= 0)
            target = this.ContainerFromIndex(this.SelectedIndex) as FrameworkElement;

        target ??= this.ItemsPanelRoot?.GetFirstLevelDescendantsOfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true);

        if (this.ItemsPanelRoot is WrapPanel p)
            p.MaxColumns = MaxColumns;

        if (target != null)
            selector.MoveTo(
                target,
                this);


        foreach (RadioButton rb in this.ItemsPanelRoot?.GetFirstLevelDescendantsOfType<RadioButton>() ?? Enumerable.Empty<RadioButton>())
        {
            rb.Checked -= OnRadioButtonChecked;
            rb.PointerEntered -= Element_PointerEntered;
            rb.PointerExited -= Element_PointerExited;
            rb.Tapped -= Rb_Tapped;

            rb.Checked += OnRadioButtonChecked;
            rb.PointerEntered += Element_PointerEntered;
            rb.PointerExited += Element_PointerExited;
            rb.Tapped += Rb_Tapped;
        }
    }

    private void UXRadioButtons_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_itemsSourceToken != 0)
        {
            this.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, _itemsSourceToken);
            _itemsSourceToken = 0;
        }

        this.ReleasePointerCaptures();

        foreach (RadioButton rb in this.ItemsPanelRoot?.GetFirstLevelDescendantsOfType<RadioButton>() ?? Enumerable.Empty<RadioButton>())
        {
            rb.Checked -= OnRadioButtonChecked;
            rb.PointerEntered -= Element_PointerEntered;
            rb.PointerExited -= Element_PointerExited;
            rb.Tapped -= Rb_Tapped;
        }

        if (SelectorVisualElement.GetElement(this) is { } selector)
        {
            selector.Target = null;
            selector.DisplayTarget = null;
        }

        if (Properties.GetZoomHelper(this) is { } zoomHelper)
        {
            zoomHelper.Detach(this);
            Properties.SetZoomHelper(this, null);
        }
    }

    protected override void OnApplyTemplate()
    {
        if (this.ItemsPanel is null)
            this.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load(
                "<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:toolkit='using:Microsoft.Toolkit.Uwp.UI.Controls'><toolkit:WrapPanel /></ItemsPanelTemplate>");

        base.OnApplyTemplate();
        UpdatePanelSpacing();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdatePanelSpacing();
        return base.MeasureOverride(availableSize);
    }

    internal void UpdatePanelSpacing()
    {
        if (this.ItemsPanelRoot is WrapPanel wrap)
        {
            wrap.HorizontalSpacing = this.ColumnSpacing;
            wrap.VerticalSpacing = this.RowSpacing;
            wrap.Orientation = this.Orientation;
            wrap.MaxColumns = this.MaxColumns;
        }
    }

    private void OnSpacingPropertyChanged()
    {
        UpdatePanelSpacing();
    }

    static partial void OnColumnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UXRadioButtons rb)
            rb.UpdatePanelSpacing();
    }

    static partial void OnRowSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UXRadioButtons rb)
            rb.UpdatePanelSpacing();
    }




    //------------------------------------------------------
    //
    // Container Generation & Preparation
    //
    //------------------------------------------------------

    protected override bool IsItemItsOwnContainerOverride(object item) => item is RadioButton;

    protected override DependencyObject GetContainerForItemOverride() => new RadioButton();

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
       // base.PrepareContainerForItemOverride(element, item);

        if (element is RadioButton rb)
        {
            if (!ReferenceEquals(element, item))
            {
                if (this.ItemTemplate is not null)
                    rb.ContentTemplate = this.ItemTemplate;
                if (this.ItemTemplateSelector is not null)
                    rb.ContentTemplateSelector = this.ItemTemplateSelector;
                rb.Content = item;
            }

            int index = GetIndexFromContainer(element);
            if (index >= 0)
            {
                if (this.SelectedIndex < 0 && rb.IsChecked == true)
                    this.SelectedIndex = index;
                else
                    rb.IsChecked = (index == this.SelectedIndex);
            }

            rb.Checked -= OnRadioButtonChecked;
            rb.Checked += OnRadioButtonChecked;

            rb.PointerEntered -= Element_PointerEntered;
            rb.PointerEntered += Element_PointerEntered;

            rb.PointerExited -= Element_PointerExited;
            rb.PointerExited += Element_PointerExited;

            rb.Tapped -= Rb_Tapped;
            rb.Tapped += Rb_Tapped;

            ElementPrepared?.Invoke(this, new(rb, index));
        }
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        //base.ClearContainerForItemOverride(element, item);

        if (element is RadioButton rb)
        {
            rb.Checked -= OnRadioButtonChecked;
            rb.PointerEntered -= Element_PointerEntered;
            rb.PointerExited -= Element_PointerExited;
            rb.Tapped -= Rb_Tapped;
        }
    }

    private int GetIndexFromContainer(DependencyObject container)
    {
        int index = this.IndexFromContainer(container);
        if (index >= 0)
            return index;

        if (container is FrameworkElement fe && fe.DataContext is not null)
        {
            int idx = this.Items.IndexOf(fe.DataContext);
            if (idx >= 0)
                return idx;
        }

        if (this.ItemsPanelRoot is Panel panel && container is UIElement ui)
        {
            int idx = panel.Children.IndexOf(ui);
            if (idx >= 0)
                return idx;
        }

        return this.Items.IndexOf(container);
    }




    //------------------------------------------------------
    //
    // Selection Management
    //
    //------------------------------------------------------

    private void OnRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelection)
            return;

        if (sender is RadioButton rb)
        {
            int index = GetIndexFromContainer(rb);
            if (index >= 0 && this.SelectedIndex != index)
            {
                _isUpdatingSelection = true;
                try
                {
                    this.SelectedIndex = index;
                }
                finally
                {
                    _isUpdatingSelection = false;
                }
            }
        }
    }

    private void UXRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateContainersSelection();
        SelectedIndexChangedCommand?.Execute(this.SelectedIndex);
    }

    private void UpdateContainersSelection()
    {
        if (_isUpdatingSelection)
            return;

        _isUpdatingSelection = true;
        try
        {
            int count = this.Items.Count;
            int selectedIndex = this.SelectedIndex;
            for (int i = 0; i < count; i++)
            {
                if (this.ContainerFromIndex(i) is RadioButton rb)
                {
                    bool shouldBeChecked = (i == selectedIndex);
                    if (rb.IsChecked != shouldBeChecked)
                        rb.IsChecked = shouldBeChecked;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    static partial void OnLayoutTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ItemsRepeater repeater)
        {
            if (e.NewValue is DataTemplate t && t.LoadContent() is Layout l)
            {
                repeater.ClearValue(ItemsRepeater.LayoutProperty);
                repeater.Layout = l;
            }
            else
                repeater.Layout = null;
        }
    }





    //------------------------------------------------------
    //
    // Public 
    //
    //------------------------------------------------------

    public bool TryFocusOn(int index, FocusState state)
    {
        if (this.ContainerFromIndex(index) is Control element)
            return element.Focus(state);

        return false;
    }





    //------------------------------------------------------
    //
    // Drag Handling
    //
    //------------------------------------------------------

    bool _moving = false;
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        // disable auto-animate
        selector.StartMove();

        _prevPoint = e.GetCurrentPoint(this).Position.ToVector2();
        this.CapturePointer(e.Pointer);
        _moving = true;
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        this.ReleasePointerCaptures();

        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        // Renable auto-animate
        selector.EndMove();

        // see if our selection has changed
        HandleIntersect(selector);
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        if (!_moving)
        {
            base.OnPointerMoved(e);
            return;
        }

        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        // get current point
        Vector2 point = e.GetCurrentPoint(this).Position.ToVector2();

        // move container by delta
        selector.Move(point - _prevPoint);

        // store current point for next delta calculation
        _prevPoint = point;
    }

    private void HandleIntersect(SelectorVisualElement selector)
    {
        Rect bounds = selector.GetBounds();

        Rect match = Rect.Empty;
        int i = 0;
        int index = 0;
        List<RadioButton> radios = this.GetFirstLevelDescendantsOfType<RadioButton>().ToList();
        Orientation orientation = selector.Orientation;
        foreach (RadioButton rb in radios)
        {
            Rect intersect = rb.GetBoundingRect(this).Value.GetIntersection(bounds);
            if (intersect != Rect.Empty)
            {
                if (match == Rect.Empty ||
                    (orientation == Orientation.Horizontal 
                        ? match.Width < intersect.Width
                        : match.Height < intersect.Height))
                {
                    match = intersect;
                    index = i;
                }
            }
            i++;
        }

        if (match != Rect.Empty)
        {
            if (this.SelectedIndex != index)
                this.SelectedIndex = index;
            else
                selector.RadioMoveTo(this, index);
        }
    }




    //------------------------------------------------------
    //
    // PointerOver animation handling
    //
    //------------------------------------------------------

    private void Rb_Tapped(object sender, TappedRoutedEventArgs e)
    {
        SetPointerOver();
    }

    private void Element_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is DependencyObject d && GetIndexFromContainer(d) == SelectedIndex)
            SetPointerOver();
    }

    private void Element_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is DependencyObject d && GetIndexFromContainer(d) == SelectedIndex)
            SetPointerExited();
    }

    private void SetPointerOver()
    {
        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        selector.SetState(SelectorInteractionState.PointerOver);
    }

    private void SetPointerExited()
    {
        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        selector.SetState(SelectorInteractionState.None);
    }
}
