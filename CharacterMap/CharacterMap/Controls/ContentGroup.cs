using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public sealed class ContentGroup : ItemsControl
{
    public object Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(object), typeof(ContentGroup), new PropertyMetadata(null));


    public object SecondaryContent
    {
        get { return (object)GetValue(SecondaryContentProperty); }
        set { SetValue(SecondaryContentProperty, value); }
    }

    public static readonly DependencyProperty SecondaryContentProperty =
        DependencyProperty.Register(nameof(SecondaryContent), typeof(object), typeof(ContentGroup), new PropertyMetadata(null));

    public ContentGroup()
    {
        this.DefaultStyleKey = typeof(ContentGroup);
    }
}
