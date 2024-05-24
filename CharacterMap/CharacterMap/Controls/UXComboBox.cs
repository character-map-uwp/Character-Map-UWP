using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Controls;

public interface IThemeableControl
{
    void UpdateTheme();
}

public class UXComboBoxItem : ComboBoxItem, IThemeableControl
{
    public ThemeHelper _themer;
    public UXComboBoxItem()
    {
        Properties.SetStyleKey(this, "DefaultThemeComboBoxItemStyle");
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

[DependencyProperty<string>("ToolTipMemberPath")]
[DependencyProperty<double>("ContentSpacing")]
[DependencyProperty("PreContent", null, nameof(UpdateContentStates))]
[DependencyProperty("SecondaryContent", null, nameof(UpdateContentStates))]
[DependencyProperty<Orientation>("ContentOrientation")]
public partial class UXComboBox : ComboBox//, IThemeableControl
{
    public ThemeHelper _themer;
    bool _isTemplateApplied = false;
    public UXComboBox()
    {
        this.DefaultStyleKey = typeof(UXComboBox);
        Properties.SetStyleKey(this, "DefaultThemeComboBoxStyle");
        _themer = new ThemeHelper(this);
    }

    public void UpdateTheme()
    {
        _themer.Update();
    }

    protected override void OnApplyTemplate()
    {
        _isTemplateApplied = true;

        base.OnApplyTemplate();
        _themer.Update();

        UpdateContentStates();
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new UXComboBoxItem { };// { Style = ItemContainerStyle };
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (!string.IsNullOrEmpty(ToolTipMemberPath))
        {
            Binding b = new Binding { Source = item, Path = new PropertyPath(ToolTipMemberPath) };
            BindingOperations.SetBinding(element, ToolTipService.ToolTipProperty, b);
        }
    }

    void UpdateContentStates()
    {
        if (_isTemplateApplied is false)
            return;

        if (SecondaryContent is not null)
            VisualStateManager.GoToState(this, "PostContentState", false);
        else if (PreContent is not null)
            VisualStateManager.GoToState(this, "PreContentState", false);

    }
}
