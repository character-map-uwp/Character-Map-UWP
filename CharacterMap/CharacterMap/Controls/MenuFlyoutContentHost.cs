using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty("Content")]
public sealed partial class MenuFlyoutContentHost : MenuFlyoutSeparator
{
    public MenuFlyoutContentHost() => this.DefaultStyleKey = typeof(MenuFlyoutContentHost);
}
