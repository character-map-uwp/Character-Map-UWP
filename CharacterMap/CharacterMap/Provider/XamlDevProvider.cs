﻿using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public class XamlDevProvider : DevProviderBase
    {
        public XamlDevProvider(CharacterRenderingOptions o, Character c) : base(o, c)
        {
            DisplayName = "XAML (UWP)";
        }

        protected override DevProviderType GetDevProviderType() => DevProviderType.XAML;
        protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
        protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();

        IReadOnlyList<DevOption> Inflate()
        {
            var v = Options.Variant;
            var c = Character;

            bool hasSymbol = FontFinder.IsSystemSymbolFamily(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
            var hex = c.UnicodeIndex.ToString("x4").ToUpper();

            string pathIconData = GetOutlineGeometry(c, Options);

            var ops = new List<DevOption>()
            {
                new ("TxtXamlCode/Header", $"&#x{hex};"),
                new ("TxtFontIcon/Header", $@"<FontIcon FontFamily=""{GetFontSource(v?.XamlFontSource)}"" Glyph=""&#x{hex};"" />", supportsTypography: true),
            };

            if (!string.IsNullOrWhiteSpace(pathIconData))
                ops.Add(new DevOption("TxtPathIcon/Text", $"<PathIcon Data=\"{pathIconData}\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" />", supportsTypography: true));

            if (hasSymbol)
                ops.Add(new DevOption("TxtSymbolIcon/Header", $@"<SymbolIcon Symbol=""{(Symbol)c.UnicodeIndex}"" />"));

            return ops;
        }

        private string GetFontSource(string fontSource)
        {
            if (fontSource == "Segoe MDL2 Assets")
                return "{ThemeResource SymbolThemeFontFamily}";
            return fontSource;
        }
    }
}
