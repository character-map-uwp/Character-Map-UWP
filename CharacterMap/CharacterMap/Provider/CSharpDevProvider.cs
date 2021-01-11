using CharacterMap.Core;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public class CSharpDevProvider : DevProviderBase
    {
        public CSharpDevProvider(CharacterRenderingOptions r, Character c) : base(r, c) {
            DisplayName = "C# (UWP)";
        }

        protected override DevProviderType GetDevProviderType() => DevProviderType.CSharp;

        protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
        protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

        IReadOnlyList<DevOption> Inflate()
        {
            var v = Options.Variant;
            var c = Character;

            bool hasSymbol = FontFinder.IsSegoeMDL2(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
            var hex = c.UnicodeIndex.ToString("x4").ToUpper();

            string pathIconData = GetOutlineGeometry(c, Options);

            var ops = new List<DevOption>()
            {
                new ("TxtXamlCode/Header", c.UnicodeIndex > 0xFFFF ? $"\\U{c.UnicodeIndex:x8}".ToUpper() : $"\\u{hex}"),
                new ("TxtFontIcon/Header", $"new FontIcon {{ FontFamily = new Windows.UI.Xaml.Media.FontFamily(\"{v?.XamlFontSource}\") , Glyph = \"\\u{hex}\" }};"),
            };

            if (!string.IsNullOrWhiteSpace(pathIconData))
                ops.Add(new DevOption("TxtPathIcon/Text", $"new PathIcon {{ Data = (Windows.UI.Xaml.Media.Geometry)Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Windows.UI.Xaml.Media.Geometry), \"{pathIconData}\"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }};",
                    supportsTypography: true));

            if (hasSymbol)
                ops.Add(new DevOption("TxtSymbolIcon/Header", $"new SymbolIcon {{ Symbol = Symbol.{(Symbol)c.UnicodeIndex} }};"));

            return ops;
        }
    }
}
