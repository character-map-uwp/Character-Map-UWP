using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

public enum ContentPlacement
{
    Right = 0,
    Bottom = 1
}


[ContentProperty(Name = nameof(Content))]
[DependencyProperty("Title")]
[DependencyProperty("Content", null, nameof(UpdatePlacementStates))]
[DependencyProperty("Description", null, nameof(UpdateDescriptionStates))]
[DependencyProperty("Icon", null, nameof(UpdateIconStates))]
[DependencyProperty<double>("IconSize", 24d)]
[DependencyProperty<ContentPlacement>("ContentPlacement", ContentPlacement.Right)]
[DependencyProperty<bool>("HasItems", typeof(bool))]
[DependencyProperty<CornerRadius>("BottomCornerRadius", "new CornerRadius()")]
public sealed partial class SettingsPresenter : ItemsControl, IThemeableControl
{
    private ThemeHelper _themer;

    private FrameworkElement _itemsRoot = null;

    public SettingsPresenter()
    {
        Properties.SetStyleKey(this, "DefaultSettingsPresenterStyle");
        this.DefaultStyleKey = typeof(SettingsPresenter);
        _themer = new ThemeHelper(this);
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => false;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        UpdateCornerRadius();
    }

    protected override void OnItemsChanged(object e)
    {
        if (e is not null)
            base.OnItemsChanged(e);

        HasItems = Items.Count > 0;

        if (HasItems)
        {
            if (_itemsRoot is null)
            {
                // force x:Load on ItemsPresenter
                _itemsRoot = this.GetTemplateChild("ItemsRoot") as FrameworkElement;
            }

            VisualStateManager.GoToState(this, "HasItemsState", ResourceHelper.AllowAnimation);
            if (_itemsRoot is not null
                && e is not null
                && ResourceHelper.AllowAnimation
                && VisualTreeHelperExtensions.GetImplementationRoot(_itemsRoot) is FrameworkElement target)
            {
                Visual v = target.EnableCompositionTranslation().GetElementVisual();
                var ease = v.Compositor.GetCachedFluentEntranceEase();
                v.StartAnimation(
                    v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                        .AddKeyFrame(0, "Vector3(0, -this.Target.Size.Y - 8, 0)")
                        .AddKeyFrame(1, new Vector3(), ease)
                        .SetDelay(0, AnimationDelayBehavior.SetInitialValueBeforeDelay)
                        .SetDuration(0.4));
            }

            UpdateCornerRadius();
        }
        else
            VisualStateManager.GoToState(this, "NoItemsState", ResourceHelper.AllowAnimation);
    }

    private void UpdateCornerRadius()
    {
        if (this.ItemsPanelRoot is null)
            return;

        foreach (var item in this.ItemsPanelRoot.Children)
        {
            if (item is ContentPresenter f)
            {
                if (f.Content == this.Items.Last())
                    f.CornerRadius = this.BottomCornerRadius;
                else
                    f.CornerRadius = new CornerRadius();
            }
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdatePlacementStates();
        UpdateIconStates();
        UpdateDescriptionStates();

        OnItemsChanged(null);
        //_themer.Update();
    }

    void UpdatePlacementStates()
    {
        VisualStateManager.GoToState(this, $"{ContentPlacement}PlacementState", ResourceHelper.AllowAnimation);
    }

    void UpdateIconStates()
    {
        string state = Icon is null ? "NoIconState" : "IconState";
        VisualStateManager.GoToState(this, state, ResourceHelper.AllowAnimation);
    }

    private void UpdateDescriptionStates()
    {
        string state = Description is null ? "NoDescriptionState" : "DescriptionState";
        VisualStateManager.GoToState(this, state, ResourceHelper.AllowAnimation);
    }

    public void UpdateTheme()
    {
        ResourceHelper.TryResolveThemeStyle3(this);
    }
}
