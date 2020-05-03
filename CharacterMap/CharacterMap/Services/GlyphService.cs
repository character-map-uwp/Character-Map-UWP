//#define GENERATE_DATA

using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
using CharacterMapCX;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Graphics.Canvas.Text;
using SQLite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Services
{
    public interface IGlyphData
    {
        [Column("Ix")]
        int UnicodeIndex { get; }
        [Column("Hx")]
        string UnicodeHex { get; }
        string Description { get; }
    }

    public class GlyphDescription : IGlyphData
    {
        [PrimaryKey, Column("Ix")]
        public int UnicodeIndex { get; set; }

        [Indexed, MaxLength(5), Column("Hx")]
        public string UnicodeHex { get; set; }

        public string Description { get; set; }
    }

    public class MDL2Glyph : GlyphDescription { }
    public class MaterialDesignIconsLegacyGlyph : GlyphDescription { }
    public class MaterialDesignIconsGlyph : GlyphDescription { }
    public class WebdingsGlyph : GlyphDescription { }
    public class WingdingsGlyph : GlyphDescription { }
    public class Wingdings2Glyph : GlyphDescription { }
    public class Wingdings3Glyph : GlyphDescription { }
    public class FontAwesomeGlyph : GlyphDescription { }
    public class IcoFontGlyph : GlyphDescription { }

    public interface IGlyphDataProvider
    {
        void Initialise();
        string GetCharacterDescription(int unicodeIndex, FontVariant variant);
        Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant);
    }

    public static class GlyphService
    {
        private static IGlyphDataProvider _provider { get; set; }

        private static Task _init { get; set; }

        public static IReadOnlyList<IGlyphData> EMPTY_SEARCH = new List<IGlyphData>();

        static GlyphService()
        {
            _provider = new SQLiteGlyphProvider();
            _provider.Initialise();
        }

        public static Task InitializeAsync()
        {
            return _init ?? (_init = InitializeInternalAsync());
        }

        private static Task InitializeInternalAsync()
        {
#if DEBUG && GENERATE_DATABASE
            if (_provider is SQLiteGlyphProvider p)
            {
                return p.InitialiseDatabaseAsync();
            }
#endif
            return Task.CompletedTask;
        }

        internal static string GetCharacterDescription(uint unicodeIndex, FontVariant variant)
        {
            if (variant == null || _provider == null)
                return null;

            return _provider.GetCharacterDescription((int)unicodeIndex, variant);
        }

        internal static string GetCharacterKeystroke(uint unicodeIndex)
        {
            if (unicodeIndex >= 128 && unicodeIndex <= 255)
                return Localization.Get("CharacterKeystrokeLabel",  unicodeIndex);

            return null;
        }

        internal static Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant)
        {
            if (variant == null)
                return Task.FromResult(EMPTY_SEARCH);

            return _provider.SearchAsync(query, variant);
        }

        public static (string Hex, string FontIcon, string Path, string Symbol) GetDevValues(
            Character c, FontVariant v, CanvasTextLayoutAnalysis a, CanvasTypography t, bool isXaml)
        {
            if (v == FontFinder.DefaultFont.DefaultVariant)
                return (string.Empty, string.Empty, string.Empty, string.Empty);

            Interop interop = SimpleIoc.Default.GetInstance<Interop>();

            string h, f, p, s = null;
            bool hasSymbol = FontFinder.IsMDL2(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);
            
            // Add back in future build
            //string pathData;
            //using (var geom = ExportManager.CreateGeometry(ResourceHelper.AppSettings.GridSize, v, c, a, t))
            //{
            //    pathData = interop.GetPathData(geom).Path;
            //}

            string pathIconData;
            using (var geom = ExportManager.CreateGeometry(20, v, c, a, t))
            {
                pathIconData = interop.GetPathData(geom).Path;
            }

            var hex = c.UnicodeIndex.ToString("x4").ToUpper();
            if (isXaml)
            {
                h = $"&#x{hex};";
                f = $@"<FontIcon FontFamily=""{v.XamlFontSource}"" Glyph=""&#x{hex};"" />";
                p = $"<PathIcon Data=\"{pathIconData}\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" />";

                if (hasSymbol)
                    s = $@"<SymbolIcon Symbol=""{(Symbol)c.UnicodeIndex}"" />";
            }
            else
            {
                h = c.UnicodeIndex > 0xFFFF ? $"\\U{c.UnicodeIndex:x8}".ToUpper() : $"\\u{hex}";
                f = $"new FontIcon {{ FontFamily = new Windows.UI.Xaml.Media.FontFamily(\"{v.XamlFontSource}\") , Glyph = \"\\u{hex}\" }};";
                p = $"new PathIcon {{ Data = (Windows.UI.Xaml.Media.Geometry)Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), \"{pathIconData}\"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }};";

                if (hasSymbol)
                    s = $"new SymbolIcon {{ Symbol = Symbol.{(Symbol)c.UnicodeIndex} }};";
            }

            return (h, f, p, s);
        }

    }
}
