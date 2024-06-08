using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty<ListView>("Target")]
public partial class PreviewTip : ContentControl
{

    ListView _parent = null;

    Debouncer _debouncer = new();

    Visual _v = null;

    bool _isOpen = false;

    Vector3Transition _transition { get; }

    public PreviewTip()
    {
        this.DefaultStyleKey = typeof(PreviewTip);
        this.Loaded += OnLoaded;

        _transition = new Vector3Transition()
        {
            Components = Vector3TransitionComponents.X | Vector3TransitionComponents.Y,
            Duration = TimeSpan.FromSeconds(0.2)
        };
    }

    protected override void OnApplyTemplate()
    {
        VisualStateManager.GoToState(this, "Closed", false);
        _v = ((FrameworkElement)this.GetTemplateChild("LayoutRoot")).EnableCompositionTranslation().GetElementVisual();
    }

    private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
        _parent = Target;
        AttachTo(Target);
    }

    public void AttachTo(ListView listView)
    {
        _parent = listView;

        listView.PointerCanceled += ListView_PointerCanceled;
        listView.PointerExited += ListView_PointerExited;
        listView.PointerCaptureLost += ListView_PointerCaptureLost;
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

    private void ListView_PointerCaptureLost(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Hide();
    }

    private void ListView_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Hide();
    }

    private void ListView_PointerCanceled(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Hide();
    }

    void Trigger(SelectorItem item)
    {
        if (_isOpen is false)
        {
            _debouncer.Debounce(900, () =>
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
        _isOpen = VisualStateManager.GoToState(this, "Opened", true);
    }

    void MoveTo(SelectorItem item)
    {
        var rect = item.GetBoundingRect((FrameworkElement)Window.Current.Content);
        this.Content = item.Content;
        this.TranslationTransition = _isOpen ? _transition : null; ;
        this.Translation = new Vector3(0, (float)rect.Value.Top, 30f);
    }

    void Hide()
    {
        _debouncer.Cancel();
        _isOpen = !VisualStateManager.GoToState(this, "Closed", true);
    }
}