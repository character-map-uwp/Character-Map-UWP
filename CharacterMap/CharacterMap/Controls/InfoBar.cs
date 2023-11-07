using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public sealed class InfoBar : ContentControl
{
    public object SecondaryContent
    {
        get { return (object)GetValue(SecondaryContentProperty); }
        set { SetValue(SecondaryContentProperty, value); }
    }

    public static readonly DependencyProperty SecondaryContentProperty =
        DependencyProperty.Register(nameof(SecondaryContent), typeof(object), typeof(InfoBar), new PropertyMetadata(null));

    public InfoBar()
    {
        this.DefaultStyleKey = typeof(InfoBar);
    }
}
