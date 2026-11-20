using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty("TitleBarContent", null, nameof(OnTitleContentChanged))]
[DependencyProperty("TitleRightContent")]
[DependencyProperty<bool>("AllowShadows")]
[DependencyProperty<bool>("IsWindowRoot", false, nameof(UpdateTitleBarHitTest))]
[DependencyProperty<string>("Title")]
[DependencyProperty<string>("Description")]
[DependencyProperty<Visibility>("CloseButtonVisibility")]
[DependencyProperty<Visibility>("HeaderVisibility")]
[DependencyProperty<GridLength>("TitleBarHeight", "new GridLength(32)")]
[DependencyProperty<Brush>("TitleBackgroundBrush")]
[DependencyProperty<Brush>("ContentBackground")]
[DependencyProperty<string>("CloseGlyph")]
[DependencyProperty<bool>("IsCloseAlignedLeft", false, nameof(UpdateClose))]
[DependencyProperty<BrushTransition>("BackgroundTransition")]

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

        UpdateTitleBarHitTest();

        OnTitleContentChanged();
        OnTitleRightContentChanged(null, TitleRightContent);
        UpdateClose();
    }

    public UIElement GetTitleContainerElement()
    {
        return this.GetTemplateChild("TitleContainer") as UIElement;
    }

    private void OnTitleContentChanged()
    {
        if (this.GetTemplateChild("TitleBarPresenter") is ContentPresenter c)
            c.Content = this.TitleBarContent ?? new Border();
    }

    partial void OnTitleRightContentChanged(object o, object n)
    {
        if (this.GetTemplateChild("TitleRightContentPresenter") is ContentPresenter c)
            c.Content = n ?? new Border();
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
            e.IsHitTestVisible = true;
            TitleBarHelper.SetTranisentTitleBar(e);
        }
    }

    public void ClearTitleBar()
    {
        if (this.GetTemplateChild("TitleBackground") is FrameworkElement e)
            e.IsHitTestVisible = false;
    }

    public void RestoreTitleBar()
    {
        if (this.GetTemplateChild("TitleBackground") is FrameworkElement e)
            TitleBarHelper.RestoreDefaultTitleBar(e);
    }

    private void UpdateTitleBarHitTest()
    {
        if (this.GetTemplateChild("TitleBackground") is FrameworkElement f)
        {
            if (IsWindowRoot)
            {
                f.IsHitTestVisible = true;
                TitleBarHelper.SetTitleBar(f);
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            }
            else
                f.IsHitTestVisible = false;
        }
    }

    public void SetWindowTitleBar()
    {
        this.ApplyTemplate();
        if (this.GetTemplateChild("TitleBackground") is FrameworkElement e)
        {
            e.IsHitTestVisible = true;
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
