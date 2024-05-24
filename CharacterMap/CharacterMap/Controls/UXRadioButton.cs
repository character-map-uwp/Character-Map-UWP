using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty<IconElement>("Icon")]
public partial class MenuButton : UXRadioButtonBase
{
    public MenuButton()
    {
        Properties.SetStyleKey(this, "DefaultMenuButtonStyle");
        UpdateTheme();
    }
}

public class UXRadioButton : UXRadioButtonBase
{
    public UXRadioButton()
    {
        Properties.SetStyleKey(this, "DefaultThemeRadioButtonStyle");
    }
}


public abstract class UXRadioButtonBase : RadioButton, IThemeableControl
{
    public ThemeHelper _themer;
    public UXRadioButtonBase()
    {
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
