using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public class UXSlider : Slider, IThemeableControl
{
    public ThemeHelper _themer;
    public UXSlider()
    {
        Properties.SetStyleKey(this, "DefaultThemeSliderStyle");
        _themer = new ThemeHelper(this);
    }

    public void UpdateTheme()
    {
        _themer.Update();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _themer.Update();
    }
}

public class UXHyperlinkButton : HyperlinkButton, IThemeableControl
{
    public ThemeHelper _themer;
    public UXHyperlinkButton()
    {
        Properties.SetStyleKey(this, "DefaultHyperlinkButtonStyle");
        _themer = new ThemeHelper(this);
    }

    public void UpdateTheme()
    {
        _themer.Update();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _themer.Update();
    }
}
