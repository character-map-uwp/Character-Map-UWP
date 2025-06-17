using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

/// <summary>
/// Extends RadioButtons with native selector visual support and additional useful events
/// </summary>
[AttachedProperty<double>("ColumnSpacing")]
[AttachedProperty<double>("RowSpacing")]
public partial class UXRadioButtons : RadioButtons
{
    public event TypedEventHandler<UXRadioButtons, ItemsRepeaterElementPreparedEventArgs> ElementPrepared;

    private ItemsRepeater _innerRepeater = null;

    Vector2 _prevPoint = Vector2.Zero;

    public UXRadioButtons()
    {
        this.DefaultStyleKey = typeof(RadioButtons);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_innerRepeater is not null)
        {
            _innerRepeater.ElementPrepared -= OnElementPrepared;
            _innerRepeater = null;
        }

        if (this.GetTemplateChild("InnerRepeater") is ItemsRepeater repeater)
        {
            repeater.ElementPrepared -= OnElementPrepared;
            repeater.ElementPrepared += OnElementPrepared;

            _innerRepeater = repeater;
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

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        this.ElementPrepared?.Invoke(this, args);

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

    // TODO : Handle PointerOver if selection changes by ScrollWheel




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

        var point = e.GetCurrentPoint(this).Position.ToVector2();

        // move container
        selector.MoveX((point - _prevPoint).X);
        _prevPoint = point;
    }

    private void HandleIntersect(SelectorVisualElement selector)
    {
        var bounds = selector.GetBounds();

        Rect match = Rect.Empty;
        int i = 0;
        int index = 0;
        var radios = this.GetFirstLevelDescendantsOfType<RadioButton>().ToList();
        foreach (var rb in radios)
        {
            var intersect = rb.GetBoundingRect(this).Value.GetIntersection(bounds);
            if (intersect != Rect.Empty)
            {
                if (match == Rect.Empty || match.Width < intersect.Width)
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
        if (_innerRepeater is not null)
            SetPointerOver();
    }

    private void Element_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_innerRepeater is not null 
            && _innerRepeater.GetElementIndex(sender as UIElement) == SelectedIndex)
            SetPointerOver();
    }

    private void Element_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_innerRepeater is not null
            && _innerRepeater.GetElementIndex(sender as UIElement) == SelectedIndex)
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
