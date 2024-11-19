using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty("TitleBarContent", null, nameof(OnTitleContentChanged))]
[DependencyProperty<bool>("AllowShadows")]
[DependencyProperty<bool>("IsWindowRoot")]
[DependencyProperty<string>("Title")]
[DependencyProperty<Visibility>("CloseButtonVisibility")]
[DependencyProperty<Visibility>("HeaderVisibility")]
[DependencyProperty<GridLength>("TitleBarHeight", "new GridLength(32)")]
[DependencyProperty<Brush>("TitleBackgroundBrush")]
[DependencyProperty<Brush>("ContentBackground")]
[DependencyProperty<string>("CloseGlyph")]
[DependencyProperty<bool>("IsCloseAlignedLeft", false, nameof(UpdateClose))]
public sealed partial class ModalPagePresenter : ContentControl
{
    public event RoutedEventHandler CloseClicked;

    public ModalPagePresenter()
    {
        this.DefaultStyleKey = typeof(ModalPagePresenter);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (AllowShadows)
        {
            FrameworkElement tb = (FrameworkElement)this.GetTemplateChild("TitleBackground");
            FrameworkElement cr = (FrameworkElement)this.GetTemplateChild("ContentRoot");
            CompositionFactory.SetThemeShadow(cr, 40, tb);
        }

        if (this.GetTemplateChild("BtnClose") is Button close)
        {
            close.Click -= Close_Click;
            close.Click += Close_Click;
        }

        if (this.GetTemplateChild("TitleBackground") is FrameworkElement f && IsWindowRoot)
        {
            TitleBarHelper.SetTitleBar(f);
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
        }

        OnTitleContentChanged();
        UpdateClose();
    }

    public UIElement GetTitleElement()
    {
        return this.GetTemplateChild("TitleHeader") as UIElement;
    }

    private void OnTitleContentChanged()
    {
        if (this.GetTemplateChild("TitleBarPresenter") is ContentPresenter c)
        {
            c.Content = this.TitleBarContent ?? new Border();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CloseClicked?.Invoke(this, e);
    }

    public void SetTitleBar()
    {
        this.ApplyTemplate();
        if (this.GetTemplateChild("TitleBackground") is FrameworkElement e)
        {
            TitleBarHelper.SetTranisentTitleBar(e);
        }
    }

    public void SetWindowTitleBar()
    {
        this.ApplyTemplate();
        if (this.GetTemplateChild("TitleBackground") is FrameworkElement e)
        {
            e.Measure(new Windows.Foundation.Size(32, 32));
            TitleBarHelper.SetTitleBar(e);
        }
    }

    public void SetDefaultFocus()
    {
        this.ApplyTemplate();
        if (this.GetTemplateChild("BtnClose") is Button close)
        {
            close.Focus(FocusState.Programmatic);
        }
    }

    public void GetAnimationTargets()
    {
        this.ApplyTemplate();
    }

    void UpdateClose()
    {
        VisualStateManager.GoToState(this, IsCloseAlignedLeft ? "LeftAligned" : "RightAligned", true);
    }
}
