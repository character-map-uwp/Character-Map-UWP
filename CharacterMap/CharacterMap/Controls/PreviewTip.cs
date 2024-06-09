using Windows.ApplicationModel;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

public partial class PreviewTip : ContentControl
{
    public ListView Target { get; set; }

    ListView _parent = null;

    Debouncer _debouncer = new();

    Visual _v = null;

    Vector3Transition _transition { get; }

    FrameworkElement _root = null;

    public PreviewTip()
    {
        this.DefaultStyleKey = typeof(PreviewTip);
        this.Loaded += OnLoaded;
        _v = this.EnableCompositionTranslation().GetElementVisual();
    }
    //partial void OnIsEnabledChanged(bool o, bool n)
    //{
    //    if (n is false)
    //        Hide();
    //}

    protected override void OnApplyTemplate()
    {
        _root = this.GetTemplateChild("LayoutRoot") as FrameworkElement;
    }

    private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
        _parent = Target;
        AttachTo(Target);
    }

    public void AttachTo(ListView listView)
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
            //_debouncer.Debounce(50, () => MoveTo(item));
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
                _v.StartAnimation(
                   _v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                      .AddKeyFrame(0, new Vector3(0.7f, 0.7f, 1f))
                      .AddKeyFrame(1, new Vector3(1f), CubicBezierPoints.FluentEntrance)
                      .SetDuration(0.5));

                _v.StartAnimation(
                     _v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                        .AddKeyFrame(0, 0)
                        .AddKeyFrame(1, 1, _v.Compositor.GetLinearEase())
                        .SetDuration(0.2));
            }

        }
    }

    void MoveTo(SelectorItem item)
    {
        var rect = item.GetBoundingRect((FrameworkElement)Window.Current.Content);
        this.Content = item.Content;
        //this.TranslationTransition = _isOpen ? _transition : null; ;
        Vector3 t = new Vector3(0f, (float)rect.Value.Top, 0f);

        if (_root.Visibility is Visibility.Visible)
            _v.Properties.StartAnimation(
                _v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                    .AddKeyFrame(1, t)
                    .SetDuration(0.1));
        else
            _v.SetTranslation(t);

       // _v.SetTranslation(0, );
    }

    void Hide()
    {
        _debouncer.Cancel();

        if (_root is null)
            return;

        if (_root.Visibility is Visibility.Visible)
        {
            var s =_v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                        .AddKeyFrame(1, new Vector3(0.7f, 0.7f, 1f), CubicBezierPoints.FluentAccelerate)
                        .SetDuration(0.2);

            var o = _v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                        .AddKeyFrame(0.5f, 1)
                        .AddKeyFrame(1, 0)
                        .SetDuration(0.2);

            _root.SetHideAnimation(_v.Compositor.CreateAnimationGroup(s, o));
            _root.Visibility = Visibility.Collapsed;
        }
    }
}