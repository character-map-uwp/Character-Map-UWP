using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace CharacterMap.Core;

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class Localizer : MarkupExtension
{
    public string Key { get; set; }

    public CharacterCasing Casing { get; set; } = CharacterCasing.Normal;

    public bool ZuneTitle { get; set; }
    public bool ZuneButton { get; set; }

    protected override object ProvideValue()
    {
        string text = Localization.Get(Key);

        if (ZuneTitle)
            Casing = ResourceHelper.Get<CharacterCasing>("TitleCasing");
        else if (ZuneButton)
            Casing = CharacterCasing.Upper;

        text = Casing switch
        {
            CharacterCasing.Upper => text.ToUpper(),
            CharacterCasing.Lower => text.ToLower(),
            _ => text
        };

        return text;
    }
}
