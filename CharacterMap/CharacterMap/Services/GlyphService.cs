//#define GENERATE_DATA

using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CharacterMap.Services
{
    public interface IGlyphData
    {
#if DEBUG
        [Column("Ix")]
#endif
        int UnicodeIndex { get; }
#if DEBUG
        [Column("Hx")]
#endif
        string UnicodeHex { get; }
        string Description { get; }
    }

    public class GlyphDescription : IGlyphData
    {
#if DEBUG
        [PrimaryKey, Column("Ix")]
#endif
        public int UnicodeIndex { get; set; }

#if DEBUG
        [Indexed, MaxLength(5), Column("Hx")]
#endif
        public string UnicodeHex { get; set; }

        public string Description { get; set; }
    }

    public class MDL2Glyph : GlyphDescription { }
    public class WebdingsGlyph : GlyphDescription { }
    public class WingdingsGlyph : GlyphDescription { }
    public class Wingdings2Glyph : GlyphDescription { }
    public class Wingdings3Glyph : GlyphDescription { }
    public class FontAwesomeGlyph : GlyphDescription { }

    public interface IGlyphDataProvider
    {
        void Initialise();
        string GetCharacterDescription(int unicodeIndex, FontVariant variant);
        string GetAdobeGlyphListMapping(string postscriptName);
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
            return _init ??= InitializeInternalAsync();
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

        internal static string TryGetAGLFNName(string aglfn)
        {
            return _provider.GetAdobeGlyphListMapping(aglfn);
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

        private static IReadOnlyList<Suggestion> DefaultSuggestions { get; } = new List<Suggestion>
        {
            S("English", "The quick brown dog jumps over a lazy fox. 1234567890"),
            S("English", Localization.Get("CultureSpecificPangram/Text")),
            S("LatinAlpha", "abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ"), // Latin Alphabet
            S("CyrillicAlpha", "абвгдеёжзийклмнопрстуфхцчшщъыьэюя АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ"), // Cyrillic Alphabet
            S("LatinSymbols", "1234567890.:,; ' \" (!?) +-*/= #@£$€%^& {~¬} [<>] |\\/"), // Common Latin symbols and numbers
            S("Vietnamese", "Do bạch kim rất quý nên sẽ dùng để lắp vô xương."), // Vietnamese,
            S("Bulgarian", "Шугав льохман държащ птицечовка без сейф и ютия"), // Bulgarian
            S("Greek", "Ταχίστη αλώπηξ βαφής ψημένη γη, δρασκελίζει υπέρ νωθρού κυνός"), // Greek
            S("Bengali", "একজাতীতো সংগঠিত প্রবণতা ও জৈববৈচিত্র্যের কলসী দেখা হয়।"), // Bengali
            S("Hindi", "ऋषियों को सताने वाले दुष्ट राक्षसों के राजा रावण का सर्वनाश करने वाले विष्णुवतार भगवान श्रीराम, अयोध्या के महाराज दशरथ के बड़े सपुत्र थे।"), // Hindi
            S("Thai", "เป็นมนุษย์สุดประเสริฐเลิศคุณค่า กว่าบรรดาฝูงสัตว์เดรัจฉาน จงฝ่าฟันพัฒนาวิชาการ อย่าล้างผลาญฤๅเข่นฆ่าบีฑาใคร ไม่ถือโทษโกรธแช่งซัดฮึดฮัดด่า หัดอภัยเหมือนกีฬาอัชฌาสัย ปฏิบัติประพฤติกฎกำหนดใจ พูดจาให้จ๊ะๆ จ๋าๆ น่าฟังเอยฯ"), // Thai
            S("Hebrew", "עטלף אבק נס דרך מזגן שהתפוצץ כי חם", false), // Hebrew
            S("Arabic", "نص حكيم له سر قاطع وذو شأن عظيم مكتوب على ثوب أخضر ومغلف بجلد أزرق", false), // Arabic,
            S("Korean", "키스의 고유조건은 입술끼리 만나야 하고 특별한 기술은 필요치 않다"), // Korean
            S("ChineseTraditional", "視野無限廣，窗外有藍天"), // Chinese (Traditional)
            S("Japanese", "いろはにほへと ちりぬるを わかよたれそ つねならむ うゐのおくやま けふこえて あさきゆめみし ゑひもせす（ん）") // Japanese
        };

        private static Suggestion S(string key, string content, bool ltr = true) => new(Localization.Get($"SuggestOption{key}/Text"), content, ltr);

        public static List<Suggestion> GetRampOptions()
        {
            List<Suggestion> ops = new(DefaultSuggestions);
            ops.AddRange(ResourceHelper.AppSettings.CustomRampOptions.Select(o => S("Custom", o)));
            return ops;
        }

        //public static (string Hex, string FontIcon, string Path, string Symbol) GetDevValues(
        //    Character c, FontVariant v, CanvasTextLayoutAnalysis a, CanvasTypography t, bool isXaml)
        //{
        //    if (v == FontFinder.DefaultFont.DefaultVariant)
        //        return (string.Empty, string.Empty, string.Empty, string.Empty);

        //    NativeInterop interop = Utils.GetInterop();

        //    string h, f, p, s = null;
        //    bool hasSymbol = FontFinder.IsSegoeMDL2(v) && Enum.IsDefined(typeof(Symbol), (int)c.UnicodeIndex);

        //    // Add back in future build
        //    //string pathData;
        //    //using (var geom = ExportManager.CreateGeometry(ResourceHelper.AppSettings.GridSize, v, c, a, t))
        //    //{
        //    //    pathData = interop.GetPathData(geom).Path;
        //    //}

        //    // Creating geometry is expensive. It may be worth delaying this.
        //    string pathIconData = null;
        //    if (v != null)
        //    {
        //        using var geom = ExportManager.CreateGeometry(20, v, c, a, t);
        //        pathIconData = interop.GetPathData(geom).Path;
        //    }

        //    var hex = c.UnicodeIndex.ToString("x4").ToUpper();
        //    if (isXaml)
        //    {
        //        h = $"&#x{hex};";
        //        f = $@"<FontIcon FontFamily=""{v?.XamlFontSource}"" Glyph=""&#x{hex};"" />";
        //        p = $"<PathIcon Data=\"{pathIconData}\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" />";

        //        if (hasSymbol)
        //            s = $@"<SymbolIcon Symbol=""{(Symbol)c.UnicodeIndex}"" />";
        //    }
        //    else
        //    {
        //        h = c.UnicodeIndex > 0xFFFF ? $"\\U{c.UnicodeIndex:x8}".ToUpper() : $"\\u{hex}";
        //        f = $"new FontIcon {{ FontFamily = new Windows.UI.Xaml.Media.FontFamily(\"{v?.XamlFontSource}\") , Glyph = \"\\u{hex}\" }};";
        //        p = $"new PathIcon {{ Data = (Windows.UI.Xaml.Media.Geometry)Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), \"{pathIconData}\"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }};";

        //        if (hasSymbol)
        //            s = $"new SymbolIcon {{ Symbol = Symbol.{(Symbol)c.UnicodeIndex} }};";
        //    }

        //    return (h, f, p, s);
        //}

    }

    public class Suggestion
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public FlowDirection FlowDirection { get; set; }

        public Suggestion() { }

        public Suggestion(string title, string text, bool ltr = true)
        {
            Title = title;
            Text = text;
            FlowDirection = ltr ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        }
    }
}
