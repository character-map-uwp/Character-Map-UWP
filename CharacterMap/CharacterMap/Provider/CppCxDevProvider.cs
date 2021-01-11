using CharacterMap.Core;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public class CppCxDevProvider : DevProviderBase
    {
        public CppCxDevProvider(CharacterRenderingOptions r, Character c) : base(r, c) {
            DisplayName = "C++/CX";
        }

        protected override DevProviderType GetDevProviderType() => DevProviderType.CppCX;

        protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
        protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

        IReadOnlyList<DevOption> Inflate()
        {
            var v = Options.Variant;
            var c = Character;

            bool hasSymbol = FontFinder.IsSegoeMDL2(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
            string hex = c.UnicodeIndex.ToString("x4").ToUpper();
            string pathIconData = GetOutlineGeometry(c, Options);
            string fontIcon = $"auto f = ref new FontIcon();\n" +
                $"f->FontFamily = ref new Media::FontFamily(L\"{v?.XamlFontSource}\");\n" +
                $"f->Glyph = L\"\\u{hex}\";";

            var ops = new List<DevOption>()
            {
                new ("TxtXamlCode/Header", c.UnicodeIndex > 0xFFFF ? $"\\U{c.UnicodeIndex:x8}".ToUpper() : $"\\u{hex}"),
                new ("TxtFontIcon/Header", fontIcon, true),
            };

            if (!string.IsNullOrWhiteSpace(pathIconData))
            {
                var data = $"auto p = ref new PathIcon();\n" +
                    "p->VerticalAlignment = Windows::UI::Xaml::VerticalAlignment::Center;\n" +
                    "p->HorizontalAlignment = Windows::UI::Xaml::HorizontalAlignment::Center;\n" +
                    $"p->Data = (Geometry^)Markup::XamlBindingHelper::ConvertValue(Geometry::typeid, L\"{pathIconData}\");";
                
                ops.Add(new DevOption("TxtPathIcon/Text", data, true, true));
            }

            if (hasSymbol)
                ops.Add(new DevOption("TxtSymbolIcon/Header", $"ref new SymbolIcon(Symbol::{(Symbol)c.UnicodeIndex});"));

            return ops;
        }
    }


}
