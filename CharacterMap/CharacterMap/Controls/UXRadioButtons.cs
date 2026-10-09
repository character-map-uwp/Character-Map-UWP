using Microsoft.UI.Xaml.Controls;
using System.Collections;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;



/// <summary>
/// Extends RadioButtons with native selector visual support and additional useful events
/// </summary>
[AttachedProperty<double>("ColumnSpacing")]
[AttachedProperty<double>("RowSpacing")]
[AttachedProperty<DataTemplate>("LayoutTemplate", null)]
[DependencyProperty<bool>("ForceSelection")] // Attempt to persist the selected index between ItemsSource changes
[DependencyProperty<ItemsSelectionModel>("SelectedItemsSource")] 
public partial class UXRadioButtons : RadioButtons
{
    public ICommand SelectedIndexChangedCommand { get; set; }

    public event TypedEventHandler<UXRadioButtons, ItemsRepeaterElementPreparedEventArgs> ElementPrepared;

    public ItemsRepeater InnerRepeater { get; private set; }

    Vector2 _prevPoint = Vector2.Zero;

    long token = -1;

    Debouncer _debouncer => field ?? new(8);

    public UXRadioButtons()
    {
        this.DefaultStyleKey = typeof(RadioButtons);
        this.Loaded += UXRadioButtons_Loaded;
        this.SelectionChanged += UXRadioButtons_SelectionChanged;

       this.RegisterPropertyChangedCallback(RadioButtons.ItemsSourceProperty, ItemsSourceChanged);
    }

    partial void OnSelectedItemsSourceChanged(ItemsSelectionModel o, ItemsSelectionModel n)
    {
        this.ItemsSource = n?.ItemsSource;
        this.SelectedItem = n?.SelectedItem;
    }

    private void ItemsSourceChanged(DependencyObject sender, DP dp)
    {
        if(ItemsSource is null || !this.ForceSelection)
            return;

        _debouncer.Debounce(() =>
        {
            if (this.SelectedItem is null && this.ItemsSource is IList {Count: >0 } list)
                this.SelectedItem = list[0];

            if (SelectorVisualElement.GetElement(this) is not { } selector)
                return;


            selector.MoveTo(
                this.InnerRepeater?.GetFirstLevelDescendants().FirstOrDefault(f => f.DataContext == this.SelectedItem),
                this,
                animate: false);
        });
    }

    //int previousIndex = -1;

    //private void SelectedIndexChanged(DependencyObject sender, DP dp)
    //{
    //    if (SelectedIndex < 0 && previousIndex >= 0 && PersistSelectedIndex)
    //    {
    //        var idx = previousIndex;

    //        _debouncer.Debounce(() =>
    //        {
    //            if (ItemsSource is IList items && idx > -1 && idx <= items.Count - 1)
    //                SelectedIndex = idx;
    //        });
    //    }

    //    previousIndex = SelectedIndex;
    //}


    private void UXRadioButtons_Loaded(object sender, RoutedEventArgs e)
    {
        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        selector.MoveTo(
            this.GetFirstLevelDescendantsOfType<RadioButton>().FirstOrDefault(r => r.IsChecked.HasValue && r.IsChecked.Value),
            this);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (InnerRepeater is not null)
        {
            InnerRepeater.ElementPrepared -= OnElementPrepared;
            InnerRepeater = null;
        }

        if (this.GetTemplateChild("InnerRepeater") is ItemsRepeater repeater)
        {
            repeater.ElementPrepared -= OnElementPrepared;
            repeater.ElementPrepared += OnElementPrepared;

            InnerRepeater = repeater;
        }

        this.AddHandler(
            RadioButtons.PointerPressedEvent,
            new PointerEventHandler(UXRadioButtons_PointerPressed),
            true);

        this.AddHandler(
            RadioButtons.PointerReleasedEvent,
            new PointerEventHandler(UXRadioButtons_PointerReleased),
            true);
    }

    Binding _templateBinding = null;

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        this.ElementPrepared?.Invoke(this, args);

        //if (args.Element is RadioButton b && this.ItemTemplate is not null)
        //{
        //    _templateBinding ??= new Binding
        //    {
        //        Source = this,
        //        Path = new(nameof(ItemTemplate))
        //    };

        //    b.SetBinding(RadioButton.ContentTemplateProperty, _templateBinding);
        //    b.ContentTemplate = this.ItemTemplate as DataTemplate;
        //}

        if (args.Element is { } element)
        {
            element.PointerEntered -= Element_PointerEntered;
            element.PointerEntered += Element_PointerEntered;

            element.PointerExited -= Element_PointerExited;
            element.PointerExited += Element_PointerExited;

            if (element is RadioButton rb)
            {
                rb.Tapped -= Rb_Tapped;
                rb.Tapped += Rb_Tapped;
            }
        }
    }

    private void UXRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedIndexChangedCommand?.Execute(this.SelectedIndex);
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
        if (this.InnerRepeater is { } repeater
            && repeater.TryGetElement(index) is Control element)
            return element.Focus(state);

        return false;
    }




    //------------------------------------------------------
    //
    // Drag Handling
    //
    //------------------------------------------------------

    private void UXRadioButtons_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        this.PointerMoved -= UXRadioButtons_PointerMoved;
        this.ReleasePointerCaptures();

        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        // Renable auto-animate
        selector.EndMove();

        // see if our selection has changed
        HandleIntersect(selector);
    }

    private void UXRadioButtons_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;
        
        // disable auto-animate
        selector.StartMove();

        _prevPoint = e.GetCurrentPoint(this).Position.ToVector2();
        this.CapturePointer(e.Pointer);
        this.PointerMoved -= UXRadioButtons_PointerMoved;
        this.PointerMoved += UXRadioButtons_PointerMoved;
    }

    private void UXRadioButtons_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (SelectorVisualElement.GetElement(this) is not { } selector)
            return;

        // get current point
        var point = e.GetCurrentPoint(this).Position.ToVector2();

        // move container by delta
        selector.Move(point - _prevPoint);

        // store current point for next delta calculation
        _prevPoint = point;
    }

    private void HandleIntersect(SelectorVisualElement selector)
    {
        var bounds = selector.GetBounds();

        Rect match = Rect.Empty;
        int i = 0;
        int index = 0;
        var radios = this.GetFirstLevelDescendantsOfType<RadioButton>().ToList();
        var orientation = selector.Orientation;
        foreach (var rb in radios)
        {
            var intersect = rb.GetBoundingRect(this).Value.GetIntersection(bounds);
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

    private void Rb_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (InnerRepeater is not null)
            SetPointerOver();
    }

    private void Element_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (InnerRepeater is not null 
            && InnerRepeater.GetElementIndex(sender as UIElement) == SelectedIndex)
            SetPointerOver();
    }

    private void Element_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (InnerRepeater is not null
            && InnerRepeater.GetElementIndex(sender as UIElement) == SelectedIndex)
            SetPointerExited();
    }

    void SetPointerOver()
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
