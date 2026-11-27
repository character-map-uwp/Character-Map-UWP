using CharacterMapCX.Controls;
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

[DependencyProperty<bool>("ToggleIsEnabled", true)]
[DependencyProperty<Visibility>("OptionsVisibility", Visibility.Visible)]
public partial class GlyphBoundsSelectorTemplateSettings : DependencyObject
{
    public void Update(GlyphBoundsOptions options) 
    {
        ToggleIsEnabled = options?.IsEnabled ?? false;
    }
}

[DependencyProperty<GlyphBoundsOptions>("Options")]
[DependencyProperty<GlyphBoundsSelectorTemplateSettings>("TemplateSettings", IsReadOnly = true)]
public partial class GlyphBoundsSelector : ControlBase
{
    public GlyphBoundsSelector()
    {
        TemplateSettings = new();
        this.DefaultStyleKey = typeof(GlyphBoundsSelector);
    }

    protected override void OnLoaded()
    {
        HookOptions(Options);
    }

    protected override void OnUnloaded()
    {
        if (Options is not null)
            Options.Changed -= OnOptionsValueChanged;
    }

    private void HookOptions(GlyphBoundsOptions options)
    {
        if (!IsActualLoaded)
            return;

        if (options is not null)
        {
            options.Changed -= OnOptionsValueChanged;
            options.Changed += OnOptionsValueChanged;
        }

        TemplateSettings?.Update(options);
    }

    partial void OnOptionsChanged(GlyphBoundsOptions o, GlyphBoundsOptions n)
    {
        if (o is not null)
            o.Changed -= OnOptionsValueChanged;

        HookOptions(n);
    }

    private void OnOptionsValueChanged(object sender, object e)
    {
        if (sender is GlyphBoundsOptions options)
            TemplateSettings?.Update(options);
    }
}
