using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

public enum PreviewPlacement
{
    RightEdgeTopAligned,
    BottomEdgeLeftAligned
}

[DependencyProperty<double>("RenderedHeight")]
[DependencyProperty<double>("Offset")]
[DependencyProperty<double>("OffsetRenderedHeight")]
[DependencyProperty<Point>("OffsetRenderedPointSize", "new Point()")]
public partial class MeasuredContentControl : ContentControl
{
    public MeasuredContentControl()
    {
        this.SizeChanged += MeasuredContentControl_SizeChanged;
    }

    private void MeasuredContentControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderedHeight = e.NewSize.Height;
        OffsetRenderedHeight = e.NewSize.Height + Offset;
        OffsetRenderedPointSize = new(0, OffsetRenderedHeight);
    }
}


[AttachedProperty<PreviewTip>("RegisterWith")]
[AttachedProperty<object>("DisplayContent")]
[AttachedProperty<double>("VerticalTranslation", 0d)]
[AttachedProperty<FrameworkElement>("Ancestor", IsReadOnly =true)]
public partial class PreviewTip : ContentControl
{
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }

    public PreviewPlacement Placement { get; set; } = PreviewPlacement.RightEdgeTopAligned;
    public FrameworkElement Target { get; set; }

    Debouncer _debouncer = new();

    Visual _v = null; // Visual of control itself
    Visual _rv = null; // Visual of control's internal LayoutRoot

    FrameworkElement _parent = null;
    FrameworkElement _root = null;

    CompositionAnimationGroup _hide = null;


    static partial void OnRegisterWithChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            if (e.OldValue is PreviewTip oldTip)
                oldTip.UnregisterListener(f);

            if (e.NewValue is PreviewTip newTip)
                newTip.RegisterListener(f);
        }
    }


    public PreviewTip()
    {
        this.DefaultStyleKey = typeof(PreviewTip);
        this.Loaded += OnLoaded;
        _v = this.EnableCompositionTranslation().GetElementVisual();
    }

    private void RegisterListener(FrameworkElement f)
    {
        AttachTo(f, false);
    }

    private void UnregisterListener(FrameworkElement f)
    {
        f.PointerExited -= PointerHide;
        f.PointerCanceled -= PointerHide;
        f.PointerCaptureLost -= PointerHide;
    }

    protected override void OnApplyTemplate()
    {
        _root = this.GetTemplateChild("LayoutRoot") as FrameworkElement;
        _rv = _root.EnableCompositionTranslation().GetElementVisual();
        
        TrySetClamping();

        // Prepare closing animation
        var s = _v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                       .AddKeyFrame(1, new Vector3(0.3f, 0.3f, 1f), CubicBezierPoints.FluentAccelerate)
                       .SetDuration(0.15);

        var o = _v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                    .AddKeyFrame(0.5f, 1)
                    .AddKeyFrame(1, 0, _v.Compositor.GetLinearEase())
                    .SetDuration(0.15);

        _hide = _v.Compositor.CreateAnimationGroup(s, o);
    }

    private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
        FrameworkElement target = null;

        if (Target is RadioButtons buttons)
            target = buttons;
        else
        {
            ListViewBase listView = Target as ListViewBase;
            listView ??= Target?.GetFirstDescendantOfType<ListViewBase>();
            target = listView;
        }

        if (target is not null)
        {
            _parent = target;
            AttachTo(target);
        }
    }

    public void AttachTo(FrameworkElement target, bool parent = true)
    {
        if (DesignMode.DesignModeEnabled)
            return;

        if (parent)
            _parent = target;

        target.PointerExited -= PointerHide;
        target.PointerCanceled -= PointerHide;
        target.PointerCaptureLost -= PointerHide;

        target.PointerExited += PointerHide;
        target.PointerCanceled += PointerHide;
        target.PointerCaptureLost += PointerHide;

        if (target is ListViewBase listView)
        {
            listView.SelectionChanged -= Target_SelectionChanged;
            listView.SelectionChanged += Target_SelectionChanged;

            listView.ContainerContentChanging -= ListView_ContainerContentChanging;
            listView.ContainerContentChanging += ListView_ContainerContentChanging;

            // Hook any existing containers (this path is hit in secondary windows)
            if (listView.ItemsPanelRoot is { Children.Count: > 0 } panel)
            {
                foreach (var item in panel.Children.OfType<SelectorItem>())
                {
                    item.PointerEntered -= Item_PointerEntered;
                    item.PointerEntered += Item_PointerEntered;
                }
            }
        }
        else if (target is UXRadioButtons buttons)
        {
            buttons.SelectionChanged -= Target_SelectionChanged;
            buttons.SelectionChanged += Target_SelectionChanged;

            buttons.ElementPrepared -= Buttons_ElementPrepared;
            buttons.ElementPrepared += Buttons_ElementPrepared;

            if (buttons.InnerRepeater is not null)
            {
                for (int i = 0; i < buttons.InnerRepeater.ItemsSourceView.Count; i++)
                {
                    if (buttons.InnerRepeater.ItemsSourceView.GetAt(i) is FrameworkElement item)
                    {
                        if (item is not RadioButton b)
                            b = item.GetFirstAncestorOfType<RadioButton>() ?? null;

                        item = b ?? item;

                        item.PointerEntered -= Item_PointerEntered;
                        item.PointerEntered += Item_PointerEntered;
                    }
                }
            }
        }
        else if (target is Panel p)
        {
            foreach (var child in p.Children.OfType<FrameworkElement>())
            {
                SetAncestor(child, child);
                child.PointerEntered -= Item_PointerEntered;
                child.PointerEntered += Item_PointerEntered;
            }
        }
        else
        {
            target.PointerEntered -= Item_PointerEntered;
            target.PointerEntered += Item_PointerEntered;
        }
    }




    //------------------------------------------------------
    //
    // Logic
    //
    //------------------------------------------------------

    void Trigger(FrameworkElement item)
    {
        if (IsEnabled is false)
            return;

        if (_root.Visibility is Visibility.Collapsed)
        {
            // Like a ToolTip we will only show after a short delay
            _debouncer.Debounce(800, () =>
            {
                MoveTo(item);
                Show();
            });
        }
        else
        {
            // If we're already open we move right away
            _debouncer.Debounce(0, ()=> MoveTo(item));
        }
    }

    void MoveTo(FrameworkElement item)
    {
        // TODO: This should be re-written to behave closer to how ToolTipService works:
        // if it's a UIElement it displays directly, otherwise it applies as DataContext
        // to the ItemTemplate


        var content = GetDisplayContent(item);
        content ??= item is ContentControl c ? (c.Content ?? c.DataContext) : item.DataContext;
        if (content is FrameworkElement f)
            content = f.Tag; // UXRadioButtons enters here

        this.Content = content;
        Vector3 t;

        double vertical = VerticalOffset + GetVerticalTranslation(item);
        
        if (Placement == PreviewPlacement.RightEdgeTopAligned)
        {
            var rect = item.GetBoundingRect((FrameworkElement)Window.Current.Content);

            // Let CenterPoint animation know item size
            _rv.Properties.InsertVector2("ItemSize", new Vector2((float)rect.Value.Width, (float)rect.Value.Height));
            
            // Position to the top edge of the item
            var y = (rect.Value.Top + vertical);
            t = new Vector3((float)HorizontalOffset, (float)y, 0f);
        }
        else
        {
            // This path is used for the main view TabBar
            var rect = item.GetBoundingRect((FrameworkElement)Target);
            t = new Vector3((float)(rect.Value.Left + HorizontalOffset), (float)vertical, 0f);

            // TODO: Create an automatic placement mode that displays at bottom left for pointer
            //       and at top left for touch
        }

        // If we're open we animate to the new position.
        // If we're closed set it immediately so we don't see half an animation when opening.
        if (_root.Visibility is Visibility.Visible && ResourceHelper.AllowAnimation)
            _v.Properties.StartAnimation(
                _v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                    .UseOrchestration(t));
        else
            _v.SetTranslation(t);
    }


    void TrySetClamping()
    {
        // Current logic only supports clamping for FontList
        if (Placement is not PreviewPlacement.RightEdgeTopAligned)
            return;

        var parent = ((FrameworkElement)this.Parent).GetElementVisual();
        var props = _v.Properties;

        // Clamp Y translation
        string exp = "Vector3(0f, Min(0f, p.Size.Y - rv.Size.Y - props.Translation.Y - 8), 0f)";
        _rv.Properties.StartAnimation(
            _v.CreateExpressionAnimation(CompositionFactory.TRANSLATION)
                .SetExpression(exp)
                .SetParameter("p", parent)
                .SetParameter("rv", _rv)
                .SetParameter("props", props));

        // Set centre point to sync with the middle of the highlighted item taking into 
        // account the offset induced by the clamping set above
        string exp2 = "(props.ItemSize.Y / 2f) - props.Translation.Y";
        _rv.Properties.InsertVector2("ItemSize", new Vector2(0)); // Ensure there is a default value
        _rv.StartAnimation(
            _v.CreateExpressionAnimation("CenterPoint.Y")
                .SetExpression(exp2)
                .SetParameter("props", _rv.Properties));
    }

    /// <summary>
    /// Causes the PreviewTip to be displayed (if Enabled)
    /// </summary>
    void Show()
    {
        if (IsEnabled is false)
            return;

        if (_root.Visibility is Visibility.Collapsed)
        {
            _root.Visibility = Visibility.Visible;

            if (ResourceHelper.AllowAnimation)
            {
                _rv.StartAnimation(_v.GetCached("_PreTipShowScale", () =>
                {
                    return _v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                      .AddKeyFrame(0, new Vector3(0.3f, 0.3f, 1f))
                      .AddKeyFrame(1, new Vector3(1f), CubicBezierPoints.FluentDecelerate)
                      .SetDuration(0.5);
                }));

                _rv.StartAnimation(_v.GetCached("_PreTipShowOp", () =>
                {
                    return _v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                        .AddKeyFrame(0, 0)
                        .AddKeyFrame(1, 1, _v.Compositor.GetLinearEase())
                        .SetDuration(0.1);
                }));
            }
            else
            {
                _rv.Scale = new Vector3(1);
                _rv.Opacity = 1;
            }
        }
    }

    /// <summary>
    /// Dismisses the PreviewTip
    /// </summary>
    void Hide()
    {
        _debouncer.Cancel();

        if (_root is null)
            return;

        if (_root.Visibility is Visibility.Visible)
        {
            if (ResourceHelper.AllowAnimation)
                _root.SetHideAnimation(_hide);
            else
                _root.SetHideAnimation(null);
          
            _root.Visibility = Visibility.Collapsed;
        }
    }




    //------------------------------------------------------
    //
    // Internal Events
    //
    //------------------------------------------------------

    #region Internal Events

    private void Buttons_ElementPrepared(UXRadioButtons sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        args.Element.PointerEntered -= Item_PointerEntered;
        args.Element.PointerEntered += Item_PointerEntered;
    }

    private void ListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue is false)
        {
            args.ItemContainer.PointerEntered -= Item_PointerEntered;
            args.ItemContainer.PointerEntered += Item_PointerEntered;
        }
    }

    private void Item_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        FrameworkElement target = null;
        if (sender is FrameworkElement f && GetRegisterWith(f) == this)
            target = f;

        if (target is null && sender is ContentControl cc)
            target = cc;

        if (target is null)
            target = GetAncestor((FrameworkElement)sender) ?? ((FrameworkElement)sender).GetFirstAncestorOfType<ContentControl>();

        Trigger(target);
    }

    private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Hide();
    }

    private void PointerHide(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement f && GetRegisterWith(f) == this)
            _debouncer.Debounce(100, Hide);
        else
            Hide();
    }

    #endregion
}