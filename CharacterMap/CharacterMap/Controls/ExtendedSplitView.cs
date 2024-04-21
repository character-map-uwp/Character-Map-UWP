using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty<bool>("EnableAnimation", true)]
public partial class ExtendedSplitView : SplitView
{
    FrameworkElement _contentRoot = null;
    FrameworkElement _paneRoot = null;

    public ExtendedSplitView()
    {
        this.DefaultStyleKey = typeof(ExtendedSplitView);
        this.Loaded += ExtendedSplitView_Loaded;
        this.Unloaded += ExtendedSplitView_Unloaded;
    }

    partial void OnEnableAnimationChanged(bool? oldValue, bool newValue) => UpdateAnimationStates();

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _contentRoot = this.GetTemplateChild("ContentRoot") as FrameworkElement;
        _paneRoot = this.GetTemplateChild("PaneRoot") as FrameworkElement;

        UpdateAnimationStates();
    }

    private void ExtendedSplitView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAnimationStates();
    }

    private void ExtendedSplitView_Unloaded(object sender, RoutedEventArgs e)
    {
        UpdateAnimationStates(false);
    }

    private void UpdateAnimationStates(bool allow = true)
    {
        if (_contentRoot is not null)
            Core.Properties.SetUseStandardReposition(_contentRoot, allow && EnableAnimation);

        if (_paneRoot is not null)
            Styles.Controls.SetEnableSlideOut(_paneRoot, allow && EnableAnimation);
    }
}
