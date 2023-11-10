using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public sealed class MenuFlyoutContentHost : MenuFlyoutSeparator
{
    public object Content
    {
        get { return (object)GetValue(ContentProperty); }
        set { SetValue(ContentProperty, value); }
    }

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(MenuFlyoutContentHost), new PropertyMetadata(null));


    public MenuFlyoutContentHost()
    {
        this.DefaultStyleKey = typeof(MenuFlyoutContentHost);
    }
}
