using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty("Text")]
[DependencyProperty("SecondaryContent")]

public sealed partial class ContentGroup : ItemsControl
{
    public ContentGroup()
    {
        this.DefaultStyleKey = typeof(ContentGroup);
    }
}
