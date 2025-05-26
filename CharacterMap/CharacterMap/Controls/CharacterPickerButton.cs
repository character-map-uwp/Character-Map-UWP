using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty<Control>("Target")]
[DependencyProperty<CharacterRenderingOptions>("Options")]
[DependencyProperty<FlyoutPlacementMode>("Placement", FlyoutPlacementMode.Bottom, nameof(UpdatePlacement))]
public sealed partial class CharacterPickerButton : ContentControl
{
    private static PropertyMetadata NULL_META = new(null);

    public event EventHandler<Character> CharacterSelected;

    public CharacterPickerButton()
    {
        this.DefaultStyleKey = typeof(CharacterPickerButton);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (this.GetTemplateChild("RootButton") is Button b)
        {
            if (b.Flyout is { } flyout)
            {
                flyout.Opening -= Flyout_Opening;
                flyout.Opening += Flyout_Opening;
            }
        }

        UpdatePlacement();
    }

    void UpdatePlacement()
    {
        VisualStateManager.GoToState(this, Placement == FlyoutPlacementMode.BottomEdgeAlignedRight ? "RightPlacementState" : "DefaultPlacementState", false);
    }

    private void Flyout_Opening(object sender, object e)
    {
        if (sender is Flyout { } flyout)
        {
            if (flyout.Content is CharacterPicker p)
                p.CharacterSelected -= Picker_CharacterSelected;

            p = new CharacterPicker(flyout, Options);
            p.CharacterSelected += Picker_CharacterSelected;
            flyout.Content = p;
        }
    }

    private void Picker_CharacterSelected(object sender, Character e)
    {
        if (Target is AutoSuggestBox ab)
        {
            if (ab.GetFirstDescendantOfType<TextBox>() is { } t && t.SelectionStart > -1)
            {
                string txt = ab.Text;
                int start = t.SelectionStart;
                if (t.SelectionLength > 0)
                    txt = txt.Remove(t.SelectionStart, t.SelectionLength);

                ab.Text = txt.Insert(t.SelectionStart, e.Char);
                t.SelectionStart = start + 1;
            }
        }
        if (Target is SuggestionBox sb)
            sb.Text += e.Char;
        else if (Target is TextBox box)
            box.Text += e.Char;
        else if (Target is ComboBox c)
            c.Text += e.Char;

        CharacterSelected?.Invoke(this, e);
    }
}
