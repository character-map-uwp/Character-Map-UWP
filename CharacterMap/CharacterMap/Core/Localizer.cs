using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media.Animation;

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


[MarkupExtensionReturnType(ReturnType = typeof(DWriteFontFace))]
public class DefaultFallback : MarkupExtension
{
    protected override object ProvideValue() => FontFinder.Fallback;
}

[MarkupExtensionReturnType(ReturnType = typeof(KeySpline))]
public class DefaultCompositionSpline : MarkupExtension
{
    protected override object ProvideValue() => KeySplines.CompositionDefault;
}

[MarkupExtensionReturnType(ReturnType = typeof(Duration))]
public class DefaultCompositionOffsetDuration : MarkupExtension
{
    protected override object ProvideValue() => new Duration(TimeSpan.FromSeconds(CompositionFactory.DefaultOffsetDuration));
}


[MarkupExtensionReturnType(ReturnType = typeof(TypographyFeatureInfo))]
public class DefaultTypography : MarkupExtension
{
    protected override object ProvideValue() => TypographyFeatureInfo.None;
}
