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

public partial class PreviewTip : ContentControl
{
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }

    public PreviewPlacement Placement { get; set; } = PreviewPlacement.RightEdgeTopAligned;
    public FrameworkElement Target { get; set; }

    ListViewBase _parent = null;

    Debouncer _debouncer = new();

    Visual _v = null;

    FrameworkElement _root = null;

    CompositionAnimationGroup _hide = null;

    public PreviewTip()
    {
        this.DefaultStyleKey = typeof(PreviewTip);
        this.Loaded += OnLoaded;
        _v = this.EnableCompositionTranslation().GetElementVisual();
    }

    protected override void OnApplyTemplate()
    {
        _root = this.GetTemplateChild("LayoutRoot") as FrameworkElement;
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

        listView.PointerCanceled += PointerHide;
        listView.PointerExited += PointerHide;
        listView.PointerCaptureLost += PointerHide;
        listView.ContainerContentChanging += ListView_ContainerContentChanging;
    }

    private void ListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue is false)
        {
            args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
            args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
        }
    }

    private void ItemContainer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Trigger(sender as SelectorItem);
    }

    private void PointerHide(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Hide();
    }

    void Trigger(SelectorItem item)
    {
        if (IsEnabled is false)
            return;

        if (_root.Visibility is Visibility.Collapsed)
        {
            _debouncer.Debounce(800, () =>
            {
                MoveTo(item);
                Show();
            });
        }
        else
        {
            MoveTo(item);
        }
    }

    void Show()
    {
        if (IsEnabled is false)
            return;

        if (_root.Visibility is Visibility.Collapsed)
        {
            _root.Visibility = Visibility.Visible;
            if (ResourceHelper.AllowAnimation)
            {
                _v.StartAnimation(_v.GetCached("_PreTipShowScale", () =>
                {
                    return _v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                      .AddKeyFrame(0, new Vector3(0.3f, 0.3f, 1f))
                      .AddKeyFrame(1, new Vector3(1f), CubicBezierPoints.FluentDecelerate)
                      .SetDuration(0.5);
                }));

                _v.StartAnimation(_v.GetCached("_PreTipShowOp", () =>
                {
                    return _v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                        .AddKeyFrame(0, 0)
                        .AddKeyFrame(1, 1, _v.Compositor.GetLinearEase())
                        .SetDuration(0.1);
                }));
            }
        }
    }

    void MoveTo(SelectorItem item)
    {
        this.Content = item.Content ?? item.DataContext;
        Vector3 t;
        
        if (Placement == PreviewPlacement.RightEdgeTopAligned)
        {
            var rect = item.GetBoundingRect((FrameworkElement)Window.Current.Content);
            t = new Vector3((float)HorizontalOffset, (float)(rect.Value.Top + VerticalOffset), 0f);
        }
        else
        {
            var rect = item.GetBoundingRect((FrameworkElement)Target);
            t = new Vector3((float)(rect.Value.Left + HorizontalOffset), (float)VerticalOffset, 0f);
        }

        if (_root.Visibility is Visibility.Visible && ResourceHelper.AllowAnimation)
            _v.Properties.StartAnimation(
                _v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                    .AddKeyFrame(1, t)
                    .SetDuration(0.1));
        else
            _v.SetTranslation(t);
    }

    void Hide()
    {
        _debouncer.Cancel();

        if (_root is null)
            return;

        if (_root.Visibility is Visibility.Visible)
        {
            if (ResourceHelper.AllowAnimation)
            {
                if (_hide is null)
                {
                    var s = _v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                     .AddKeyFrame(1, new Vector3(0.3f, 0.3f, 1f), CubicBezierPoints.FluentAccelerate)
                     .SetDuration(0.2);

                    var o = _v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                                .AddKeyFrame(0.5f, 1)
                                .AddKeyFrame(1, 0)
                                .SetDuration(0.2);

                    _hide = _v.Compositor.CreateAnimationGroup(s, o);
                }

                _root.SetHideAnimation(_hide);
            }
            else
            {
                _root.SetHideAnimation(null);
            }
          
            _root.Visibility = Visibility.Collapsed;
        }
    }
}