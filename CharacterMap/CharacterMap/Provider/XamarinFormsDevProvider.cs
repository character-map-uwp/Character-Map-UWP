﻿namespace CharacterMap.Provider;

public class XamarinFormsDevProvider : DevProviderBase
{
    public XamarinFormsDevProvider(CharacterRenderingOptions o, Character c) : base(o, c)
    {
        DisplayName = "Xamarin Forms";
    }

    private static List<DevOption> _allOptions { get; } = new List<DevOption>
    {
        new ("TxtXamlCode/Header", null),
        new ("TxtFontImageSource/Text", null),
        new ("TxtPathGeometry/Text", null)
    };

    public override IReadOnlyList<DevOption> GetAllOptions() => _allOptions;
    protected override DevProviderType GetDevProviderType() => DevProviderType.XamarinForms;
    protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
    protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

    IReadOnlyList<DevOption> Inflate()
    {
        var v = Options.Variant;
        var c = Character;

        var hex = c.UnicodeIndex.ToString("x4").ToUpper();
        string pathIconData = GetOutlineGeometry(c, Options);
        var ops = new List<DevOption>()
        {
            new ("TxtXamlCode/Header", $"&#x{hex};"),
            new ("TxtFontImageSource/Text", $"<FontImageSource FontFamily=\"{{OnPlatform iOS={v?.FamilyName}, Android={v?.FileName}#, UWP={v?.XamlFontSource}}}\" Glyph=\"&#x{hex};\" Size=\"20\" Color=\"Black\" />"),
        };

        if (!string.IsNullOrWhiteSpace(pathIconData))
            ops.Add(new DevOption("TxtPathGeometry/Text", $"<Path Data=\"{pathIconData}\" Fill=\"Black\" Stretch=\"Uniform\" />", supportsTypography: true));

        return ops;
    }
}
