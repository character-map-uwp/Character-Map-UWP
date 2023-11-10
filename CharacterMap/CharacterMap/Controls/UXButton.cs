using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[ObservableObject]
public partial class UXButtonTemplateSettings
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

public class UXButton : Button//, IThemeableControl
{
    public bool IsHintVisible
    {
        get { return (bool)GetValue(IsHintVisibleProperty); }
        set { SetValue(IsHintVisibleProperty, value); }
    }

    public static readonly DependencyProperty IsHintVisibleProperty =
        DependencyProperty.Register(nameof(IsHintVisible), typeof(bool), typeof(UXButton), new PropertyMetadata(false, (d, e) =>
        {
            ((UXButton)d).UpdateHint();
        }));

    public bool IsLabelVisible
    {
        get { return (bool)GetValue(IsLabelVisibleProperty); }
        set { SetValue(IsLabelVisibleProperty, value); }
    }

    public static readonly DependencyProperty IsLabelVisibleProperty =
        DependencyProperty.Register(nameof(IsLabelVisible), typeof(bool), typeof(UXButton), new PropertyMetadata(false, (d, e) =>
        {
            ((UXButton)d).UpdateLabel();
        }));

    public CharacterCasing LabelCasing
    {
        get { return (CharacterCasing)GetValue(LabelCasingProperty); }
        set { SetValue(LabelCasingProperty, value); }
    }

    public static readonly DependencyProperty LabelCasingProperty =
        DependencyProperty.Register(nameof(LabelCasing), typeof(CharacterCasing), typeof(UXButton), new PropertyMetadata(CharacterCasing.Normal, (d, e) =>
        {
            ((UXButton)d).UpdateLabelText();
        }));

    public string Label
    {
        get { return (string)GetValue(LabelProperty); }
        set { SetValue(LabelProperty, value); }
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(UXButton), new PropertyMetadata(null, (d, e) =>
        {
            ((UXButton)d).UpdateLabelText();
        }));



    public bool IsActive
    {
        get { return (bool)GetValue(IsActiveProperty); }
        set { SetValue(IsActiveProperty, value); }
    }

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(UXButton), new PropertyMetadata(false, (d, e) =>
        {
            ((UXButton)d).UpdateActive();
        }));



    public UXButtonTemplateSettings TemplateSettings { get; } = new UXButtonTemplateSettings();

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
