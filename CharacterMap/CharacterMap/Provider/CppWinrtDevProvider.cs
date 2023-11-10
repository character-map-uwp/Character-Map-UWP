using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider;

/// <summary>
/// Base for Jupiter-based C++/WinRT XAML platforms (UWP, WinUI3)
/// </summary>
public abstract class CppWinrtJupiterDevProviderBase : DevProviderBase
{
    public CppWinrtJupiterDevProviderBase(CharacterRenderingOptions r, Character c) : base(r, c) { }

    protected abstract string Namespace { get; }
    protected abstract DevProviderType DevProviderType { get; }
    protected override DevProviderType GetDevProviderType() => DevProviderType;
    protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
    protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

    IReadOnlyList<DevOption> Inflate()
    {
        var v = Options.Variant;
        var c = Character;

        bool hasSymbol = FontFinder.IsSystemSymbolFamily(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
        string hex = c.UnicodeIndex.ToString("x4").ToUpper();
        string pathIconData = GetOutlineGeometry(c, Options);

        string fontIcon =
            $"// Add \"#include <winrt/{Namespace}.UI.Xaml.Media.h>\" to pch.h\n" +
            "auto f = FontIcon();\n" +
            $"f.Glyph(L\"\\u{hex}\");\n" +
            $"f.FontFamily(Media::FontFamily(L\"{v?.XamlFontSource}\"));";

        var ops = new List<DevOption>()
        {
            new ("TxtXamlCode/Header", c.UnicodeIndex > 0xFFFF ? $"\\U{c.UnicodeIndex:x8}".ToUpper() : $"\\u{hex}"),
            new ("TxtFontIcon/Header", fontIcon, true),
        };

        if (!string.IsNullOrWhiteSpace(pathIconData))
        {
            var data =
                $"// Add \"#include <winrt/{Namespace}.UI.Xaml.Media.h>\" to pch.h\n" +
                $"auto p = PathIcon();\n" +
                $"p.VerticalAlignment(VerticalAlignment::Center);\n" +
                $"p.HorizontalAlignment(HorizontalAlignment::Center);\n" +
                $"p.Data(Markup::XamlBindingHelper::ConvertValue(winrt::xaml_typename<Geometry>(), box_value(L\"{pathIconData}\")).try_as<Geometry>());";

            ops.Add(new DevOption("TxtPathIcon/Text", data, true, true));
        }

        if (hasSymbol)
            ops.Add(new DevOption("TxtSymbolIcon/Header", $"SymbolIcon(Symbol::{(Symbol)c.UnicodeIndex});"));

        return ops;
    }
}

public class CppWinrtDevProvider : CppWinrtJupiterDevProviderBase
{
    public CppWinrtDevProvider(CharacterRenderingOptions r, Character c) : base(r, c)
    {
        DisplayName = "C++/WinRT (UWP)";
    }

    protected override string Namespace { get; } = "Windows";

    protected override DevProviderType DevProviderType { get; } = DevProviderType.CppWinRT;
}

public class CppWinrtWinUI3DevProvider : CppWinrtJupiterDevProviderBase
{
    public CppWinrtWinUI3DevProvider(CharacterRenderingOptions r, Character c) : base(r, c)
    {
        DisplayName = "C++/WinRT (WinUI 3)";
    }

    protected override string Namespace { get; } = "Microsoft";

    protected override DevProviderType DevProviderType { get; } = DevProviderType.CppWinRTWinUI3;
}
