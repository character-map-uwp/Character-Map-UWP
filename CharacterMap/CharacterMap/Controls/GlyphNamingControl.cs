using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;
public sealed class GlyphNamingControl : Control
{
    public GlyphFileNameViewModel ViewModel
    {
        get { return (GlyphFileNameViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DP ViewModelProperty = DP<GlyphFileNameViewModel, GlyphNamingControl>();

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
