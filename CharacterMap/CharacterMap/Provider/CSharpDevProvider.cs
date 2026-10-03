using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider;

/// <summary>
/// Base for Jupiter-based C# XAML platforms (UWP, WinUI3)
/// </summary>
public abstract class CSharpJupiterDevProviderBase : DevProviderBase
{
    public CSharpJupiterDevProviderBase(CharacterRenderingOptions r, Character c) : base(r, c) { }

    protected abstract string Namespace { get; }
    protected abstract DevProviderType DevProviderType { get; }
    protected override DevProviderType GetDevProviderType() => DevProviderType;
    protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
    protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

    IReadOnlyList<DevOption> Inflate()
    {
        var v = Options.Face;
        var c = Character;

        bool hasSymbol = FontFinder.IsSystemSymbolFamily(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
        var hex = c.UnicodeIndex.ToString("x4").ToUpper();

        string pathIconData = GetOutlineGeometry(c, Options);

        string fontIcon = $"new FontIcon {{ FontFamily = new {Namespace}.UI.Xaml.Media.FontFamily(\"{v?.XamlFontSource}\"), Glyph = \"\\u{hex}\" }};";
        bool hasTypography = false;

        if (GetTypographyMapping(Options) is { } m)
        {
            string csValue = string.Format(m.CodeValue, Namespace);
            fontIcon =
                $"var f = new FontIcon {{ FontFamily = new {Namespace}.UI.Xaml.Media.FontFamily(\"{v?.XamlFontSource}\"), Glyph = \"\\u{hex}\" }};\n" +
                $"{Namespace}.UI.Xaml.Documents.Typography.Set{m.PropertyName}(f, {csValue});";
            hasTypography = true;
        }

        var ops = new List<DevOption>()
        {
            new ("TxtXamlCode/Header", c.UnicodeIndex > 0xFFFF ? $"\\U{c.UnicodeIndex:x8}".ToUpper() : $"\\u{hex}"),
            new ("TxtFontIcon/Header", fontIcon, forceExtended: hasTypography, supportsTypography: true),
        };

        if (!string.IsNullOrWhiteSpace(pathIconData))
            ops.Add(new DevOption("TxtPathIcon/Text", $"new PathIcon {{ Data = ({Namespace}.UI.Xaml.Media.Geometry){Namespace}.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof({Namespace}.UI.Xaml.Media.Geometry), \"{pathIconData}\"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }};",
                supportsTypography: true));

        if (hasSymbol)
            ops.Add(new DevOption("TxtSymbolIcon/Header", $"new SymbolIcon {{ Symbol = Symbol.{(Symbol)c.UnicodeIndex} }};"));

        return ops;
    }
}

public class CSharpDevProvider : CSharpJupiterDevProviderBase
{
    public CSharpDevProvider(CharacterRenderingOptions r, Character c) : base(r, c)
    {
        DisplayName = "C# (UWP)";
    }

    protected override string Namespace { get; } = "Windows";

    protected override DevProviderType DevProviderType { get; } = DevProviderType.CSharp;
}

public class CSharpWinUI3DevProvider : CSharpJupiterDevProviderBase
{
    public CSharpWinUI3DevProvider(CharacterRenderingOptions r, Character c) : base(r, c)
    {
        DisplayName = "C# (WinUI 3)";
    }

    protected override string Namespace { get; } = "Microsoft";

    protected override DevProviderType DevProviderType { get; } = DevProviderType.CSharpWinUI3;
}
