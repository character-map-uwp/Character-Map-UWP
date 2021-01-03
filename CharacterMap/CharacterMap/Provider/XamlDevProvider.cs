using CharacterMap.Core;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public class XamlDevProvider : DevProviderBase
    {
        public XamlDevProvider(CharacterRenderingOptions o, Character c) : base(o, c) { }

        protected override DevProviderType GetDevProviderType() => DevProviderType.XAML;
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
                new ("TxtXamlCode/Header", $"&#x{hex};"),
                new ("TxtFontIcon/Header", $@"<FontIcon FontFamily=""{v?.XamlFontSource}"" Glyph=""&#x{hex};"" />"),
            };

            if (!string.IsNullOrWhiteSpace(pathIconData))
                ops.Add(new DevOption("TxtPathIcon/Text", $"<PathIcon Data=\"{pathIconData}\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" />"));

            if (hasSymbol)
                ops.Add(new DevOption("TxtSymbolIcon/Text", $@"<SymbolIcon Symbol=""{(Symbol)c.UnicodeIndex}"" />"));

            return ops;
        }
    }


}
