using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public partial class UXButtonTemplateSettings : ObservableObject
{
    [ObservableProperty]
    string _effectiveLabel;

    public void Update(string text, CharacterCasing casing)
    {
        EffectiveLabel = casing switch
        {
            CharacterCasing.Upper => text.ToUpper(),
            CharacterCasing.Lower => text.ToLower(),
            _ => text
        };
    }
}

[DependencyProperty<bool>("IsActive")]
[DependencyProperty<bool>("IsHintVisible")]
[DependencyProperty<bool>("IsLabelVisible")]
[DependencyProperty<string>("Label")]
[DependencyProperty<CharacterCasing>("LabelCasing")]
public partial class UXButton : Button//, IThemeableControl
{
    partial void OnIsActiveChanged(bool? oldValue, bool newValue) => UpdateActive();

    partial void OnIsHintVisibleChanged(bool? oldValue, bool newValue) => UpdateHint();

    partial void OnIsLabelVisibleChanged(bool? oldValue, bool newValue) => UpdateLabel();

    partial void OnLabelChanged(string oldValue, string newValue) => UpdateLabelText();

    partial void OnLabelCasingChanged(CharacterCasing? oldValue, CharacterCasing newValue) => UpdateLabelText();

    public UXButtonTemplateSettings TemplateSettings { get; } = new ();

    bool _isTemplateApplied = false;

    //public ThemeHelper _themer;

    public UXButton()
    {
        //Properties.SetStyleKey(this, "DefaultThemeButtonStyle");
        //_themer = new ThemeHelper(this);
    }


    protected override void OnApplyTemplate()
    {
        _isTemplateApplied = true;

        base.OnApplyTemplate();
        //_themer.Update();

        UpdateHint(false);
        UpdateLabel(false);
        UpdateActive(false);
    }
    private void UpdateActive(bool animate = true)
    {
        if (_isTemplateApplied)
            VisualStateManager.GoToState(this, IsActive ? "IsActive" : "IsNotActive", animate);
    }

    private void UpdateHint(bool animate = true)
    {
        if (_isTemplateApplied)
            VisualStateManager.GoToState(this, IsHintVisible ? "HintVisible" : "HintHidden", animate);
    }

    private void UpdateLabel(bool animate = true)
    {
        if (_isTemplateApplied)
            VisualStateManager.GoToState(this, IsLabelVisible ? "LabelVisible" : "LabelHidden", animate);
    }

    private void UpdateLabelText()
    {
        TemplateSettings.Update(Label, LabelCasing);
    }

    public void UpdateTheme()
    {
        //_themer.Update();
    }
}
