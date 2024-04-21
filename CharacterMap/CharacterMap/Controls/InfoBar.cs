using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty("SecondaryContent")]
public sealed partial class InfoBar : ContentControl
{
    public InfoBar()
    {
        this.DefaultStyleKey = typeof(InfoBar);
    }
}
