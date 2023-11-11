using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public class MenuButton : UXRadioButton
{
    public IconElement Icon
    {
        get { return (IconElement)GetValue(IconProperty); }
        set { SetValue(IconProperty, value); }
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(IconElement), typeof(MenuButton), new PropertyMetadata(null));

    public MenuButton()
    {
        Properties.SetStyleKey(this, "DefaultMenuButtonStyle");
        UpdateTheme();
    }
}


public class UXRadioButton : RadioButton, IThemeableControl
{
    public ThemeHelper _themer;
    public UXRadioButton()
    {
        Properties.SetStyleKey(this, "DefaultRadioButtonStyle");
        _themer = new ThemeHelper(this);
    }

    public virtual void UpdateTheme()
    {
        _themer.Update();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _themer.Update();
        UpdateSelectionStates();
    }

    private void UpdateSelectionStates()
    {
        VisualStateManager.GoToState(this, "Indeterminate", false);
        string state = this.IsChecked switch
        {
            true => "Checked",
            false => "Unchecked",
            _ => "Indeterminate"
        };
        VisualStateManager.GoToState(this, state, false);
    }
}
