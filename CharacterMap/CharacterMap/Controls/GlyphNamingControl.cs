using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty<GlyphFileNameViewModel>("ViewModel")]
public sealed partial class GlyphNamingControl : Control
{
    public GlyphNamingControl()
    {
        this.DefaultStyleKey = typeof(GlyphNamingControl);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (this.GetTemplateChild("ResetButton") is Button b)
        {
            b.Click -= B_Click;
            b.Click += B_Click;
        }

        if (this.GetTemplateChild("ToggleButton") is Button t)
        {
            t.Click -= T_Click;
            t.Click += T_Click;
        }

        if (this.GetTemplateChild("ItemsRoot") is ItemsControl c)
            c.ItemsSource = FileNameWriter.All;
    }

    private void B_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.Reset();
    }

    private void T_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleExpansion();
    }
}
