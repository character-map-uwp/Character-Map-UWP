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
    BottomEdgeLeftAligned,
    Center
}

public partial class PreviewTip : ContentControl
{
    public event EventHandler<object> ContentChanged;


    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }

    public double HorizontalPadding { get; set; }
    public double VerticalPadding { get; set; }

    public PreviewPlacement Placement { get; set; } = PreviewPlacement.RightEdgeTopAligned;
    public FrameworkElement Target { get; set; }

    Debouncer _debouncer = new();

    Visual _v = null; // Visual of control itself
    Visual _rv = null; // Visual of control's internal LayoutRoot

    ListViewBase _parent = null;
    FrameworkElement _root = null;

    CompositionAnimationGroup _hide = null;




    public PreviewTip()
    {
        this.DefaultStyleKey = typeof(PreviewTip);
        this.Loaded += OnLoaded;
        _v = this.EnableCompositionTranslation().GetElementVisual();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        ContentChanged?.Invoke(this, newContent);
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
        ListViewBase listView = Target as ListViewBase;
        listView ??= Target?.GetFirstDescendantOfType<ListViewBase>();

        if (listView is not null)
        {
            _parent = listView;
            AttachTo(listView);
        }
    }

    public void AttachTo(ListViewBase listView)
    {
        if (DesignMode.DesignModeEnabled)
            return;

        _parent = listView;

        listView.SelectionChanged += ListView_SelectionChanged;
        listView.PointerCanceled += PointerHide;
        listView.PointerExited += PointerHide;
        listView.PointerCaptureLost += PointerHide;
        listView.ContainerContentChanging += ListView_ContainerContentChanging;

        // Hook any existing containers (this path is hit in secondary windows)
        if (listView.ItemsPanelRoot is { Children.Count: > 0 } panel)
        {
            foreach (var item in panel.Children.OfType<SelectorItem>())
            {
                item.PointerEntered -= ItemContainer_PointerEntered;
                item.PointerEntered += ItemContainer_PointerEntered;
            }
        }
    }




    //------------------------------------------------------
    //
    // Logic
    //
    //------------------------------------------------------

    void Trigger(SelectorItem item)
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
            MoveTo(item);
        }
    }

    void MoveTo(SelectorItem item)
    {
        this.Content = item.Content ?? item.DataContext;
        Vector3 t;
        
        if (Placement == PreviewPlacement.RightEdgeTopAligned)
        {
            // This path is used for the main Left-hand font list

            var rect = item.GetBoundingRect((FrameworkElement)Window.Current.Content);

            // Let CenterPoint animation know item size
            _rv.Properties.InsertVector2("ItemSize", new Vector2((float)rect.Value.Width, (float)rect.Value.Height));
            
            // Position to the top edge of the item
            var y = (rect.Value.Top + VerticalOffset);
            t = new Vector3((float)HorizontalOffset, (float)y, 0f);
        }
        else if (Placement == PreviewPlacement.Center)
        {
            // This path is intended for the main character map grid

            CompositionFactory.StartCentering(_rv);

            var rect = item.GetBoundingRect((FrameworkElement)this.Parent);
            t = new(
                Math.Max((float)HorizontalPadding, (float)(rect.Value.Left+ (rect.Value.Width / 2d) - this.ActualWidth /2d)), 
                Math.Max((float)VerticalPadding, (float)(rect.Value.Top + (rect.Value.Height /2d) - this.ActualHeight / 2d)), 
                0f);

            t = t + new Vector3((float)HorizontalOffset, (float)VerticalOffset, 0f);
        }
        else
        {
            // This path is used for the main view TabBar
            var rect = item.GetBoundingRect((FrameworkElement)Target);
            t = new Vector3((float)(rect.Value.Left + HorizontalOffset), (float)VerticalOffset, 0f);
        }

        // If we're open we animate to the new position.
        // If we're closed set it immediately so we don't see half an animation when opening.
        if (_root.Visibility is Visibility.Visible && ResourceHelper.AllowAnimation)
            _v.Properties.StartAnimation(
                _v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                    .AddKeyFrame(1, t)
                    .SetDuration(0.1));
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

    private void ListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue is false)
        {
            args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
            args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
        }
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Hide();
    }

    private void ItemContainer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Trigger(sender as SelectorItem);
    }

    private void PointerHide(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Hide();
    }

    #endregion
}