using CharacterMap.Core;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public class VBDevProvider : DevProviderBase
    {
        public VBDevProvider(CharacterRenderingOptions r, Character c) : base(r, c)
        {
            DisplayName = "VB (UWP)";
        }

        protected override DevProviderType GetDevProviderType() => DevProviderType.VisualBasic;

        protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
        protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

        IReadOnlyList<DevOption> Inflate()
        {
            var v = Options.Variant;
            var c = Character;

            bool hasSymbol = FontFinder.IsSystemSymbolFamily(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
            var hex = c.UnicodeIndex.ToString("x4").ToUpper();

            string pathIconData = GetOutlineGeometry(c, Options);
            string glyph = c.UnicodeIndex > 0xFFFF ? $"ChrW(&H{$"{c.UnicodeIndex:x8}".ToUpper()})" : $"ChrW(&H{hex})";

            var ops = new List<DevOption>()
            {
                new ("TxtXamlCode/Header", glyph),
                new ("TxtFontIcon/Header", $"New FontIcon With {{ .FontFamily = New Windows.UI.Xaml.Media.FontFamily(\"{v?.XamlFontSource}\"), .Glyph = {glyph} }}"),
            };

            if (!string.IsNullOrWhiteSpace(pathIconData))
               
                ops.Add(new DevOption("TxtPathIcon/Text", $"New PathIcon With {{.Data = TryCast(Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(GetType(Windows.UI.Xaml.Media.Geometry), \"{pathIconData}\"), Windows.UI.Xaml.Media.Geometry), .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}}",
                    supportsTypography: true));

            if (hasSymbol)
                ops.Add(new DevOption("TxtSymbolIcon/Header", $"New SymbolIcon With {{ .Symbol = Symbol.{(Symbol)c.UnicodeIndex} }}"));

            return ops;
        }
    }
}
