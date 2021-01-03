using CharacterMap.Core;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public class CppWinrtDevProvider : DevProviderBase
    {
        public CppWinrtDevProvider(CharacterRenderingOptions r, Character c) : base(r, c) { }

        protected override DevProviderType GetDevProviderType() => DevProviderType.CppWinRT;

        protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
        protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

        IReadOnlyList<DevOption> Inflate()
        {
            var v = Options.Variant;
            var c = Character;

            bool hasSymbol = FontFinder.IsSegoeMDL2(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
            string hex = c.UnicodeIndex.ToString("x4").ToUpper();
            string pathIconData = GetOutlineGeometry(c, Options);
        
            string fontIcon = 
                "// Add \"#include <winrt/Windows.UI.Xaml.Media.h>\" to pch.h\n" +
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
                    $"// Add \"#include <winrt/Windows.UI.Xaml.Media.h>\" to pch.h\n" +
                    $"auto p = PathIcon();\n" +
                    $"p.VerticalAlignment(VerticalAlignment::Center);\n" +
                    $"p.HorizontalAlignment(HorizontalAlignment::Center);\n" +
                    $"p.Data(Markup::XamlBindingHelper::ConvertValue(winrt::xaml_typename<Geometry>(), box_value(L\"{pathIconData}\")).try_as<Geometry>());";

                ops.Add(new DevOption("TxtPathIcon/Text", data, true));
            }

            if (hasSymbol)
                ops.Add(new DevOption("TxtSymbolIcon/Text", $"ref new SymbolIcon(Symbol::{(Symbol)c.UnicodeIndex});"));

            return ops;
        }
    }


}
