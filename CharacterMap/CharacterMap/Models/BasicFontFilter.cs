using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMapCX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Models
{
    public partial class BasicFontFilter
    {
        public Func<IEnumerable<InstalledFont>, UserCollectionsService, IEnumerable<InstalledFont>> Query { get; }
        public string DisplayTitle { get; }
        public string FilterTitle { get; }

        public static BasicFontFilter ForRange(UnicodeRange range, string displayTitle)
        {
            return new BasicFontFilter((f, c) => f.Where(v => v.Variants.Any(v => Unicode.ContainsRange(v, range))), displayTitle);
        }

        public static BasicFontFilter SupportsScript(UnicodeRange range, string displayTitle)
        {
            return new BasicFontFilter((f, c) => f.Where(v => v.Variants.Any(v => Unicode.SupportsScript(v, range))), displayTitle);
        }

        public BasicFontFilter(Func<IEnumerable<InstalledFont>, UserCollectionsService, IEnumerable<InstalledFont>> query, string displayTitle)
        {
            Query = query;
            DisplayTitle = displayTitle;
            FilterTitle = displayTitle;
        }

        public BasicFontFilter(Func<IEnumerable<InstalledFont>, UserCollectionsService, IEnumerable<InstalledFont>> query, string displayTitle, string filterTitle)
        {
            Query = query;
            DisplayTitle = displayTitle;
            FilterTitle = filterTitle;
        }
    }

    public partial class BasicFontFilter
    {
        public static BasicFontFilter All { get; }
        = new ((f, c) => f, Localization.Get("OptionAllFonts/Text"));

        public static BasicFontFilter SymbolFonts { get; }
            = new ((f, c) => f.Where(v => v.IsSymbolFont || c.SymbolCollection.Fonts.Contains(v.Name)), Localization.Get("OptionSymbolFonts/Text"));

        public static BasicFontFilter ImportedFonts { get; }
            = new ((f, c) => f.Where(v => v.HasImportedFiles), Localization.Get("OptionImportedFonts/Text"));

        public static BasicFontFilter MonospacedFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.FontFace.IsMonospaced), Localization.Get("OptionMonospacedFonts/Text"));

        public static BasicFontFilter SerifFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.Panose.IsSerifStyle), Localization.Get("OptionSerifFonts/Text"));

        public static BasicFontFilter SansSerifFonts { get; }
           = new ((f, c) => f.Where(v => v.DefaultVariant.Panose.IsSansSerifStyle), Localization.Get("OptionSansSerifFonts/Text"));

        public static BasicFontFilter AppXFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.Source == DWriteFontSource.AppxPackage), Localization.Get("OptionAppxFonts/Text"));

        public static BasicFontFilter RemoteFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.Source == DWriteFontSource.RemoteFontProvider), Localization.Get("OptionCloudFonts/Text"));

        public static BasicFontFilter PanoseDecorativeFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.Panose.Family == PanoseFamily.Decorative), Localization.Get("OptionDecorativeFonts/Text"));

        public static BasicFontFilter PanoseScriptFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.Panose.Family == PanoseFamily.Script), Localization.Get("OptionScriptFonts/Text"));

        public static BasicFontFilter ColorFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.IsColorFont), Localization.Get("OptionColorFonts/Text"));

        public static BasicFontFilter VariableFonts { get; }
            = new ((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.HasVariations), Localization.Get("OptionVariableFonts/Text"));




        /* EMOJI */

        public static BasicFontFilter EmojiAll { get; }
            = new (
                    (f, c) => f.Where(v => v.Variants.Any(v => Unicode.ContainsEmoji(v))),
                    Localization.Get("OptionAllEmoji/Text"),
                    Localization.Get("OptionAllEmojiTitle/Text"));

        public static BasicFontFilter EmojiEmoticons { get; }
            = ForRange(UnicodeRange.Emoticons, Localization.Get("OptionEmojiEmoticons/Text"));

        public static BasicFontFilter EmojiDingbats { get; }
            = ForRange(UnicodeRange.Dingbats, Localization.Get("OptionEmojiDingbats/Text"));

        public static BasicFontFilter EmojiSymbols { get; }
            = new (
                    (f, c) => f.Where(v => v.Variants.Any(v => Unicode.ContainsEmojiSymbols(v))),
                    Localization.Get("OptionEmojiSymbols/Text"));




        /* SCRIPTS */

        public static BasicFontFilter ScriptArabic { get; }
            = ForRange(UnicodeRange.Arabic, Localization.Get("OptionScriptArabic/Text"));

        public static BasicFontFilter ScriptCyrillic { get; }
            = ForRange(UnicodeRange.Cyrillic, Localization.Get("OptionScriptCyrillic/Text"));

        public static BasicFontFilter ScriptGreekAndCoptic { get; }
            = SupportsScript(UnicodeRange.GreekAndCoptic, Localization.Get("OptionScriptGreekAndCoptic/Text"));

        public static BasicFontFilter ScriptHebrew { get; }
            = ForRange(UnicodeRange.Hebrew, Localization.Get("OptionScriptHebrew/Text"));

        public static BasicFontFilter ScriptThai { get; }
            = SupportsScript(UnicodeRange.Thai, Localization.Get("OptionScriptThai/Text"));

        public static BasicFontFilter ScriptCJKUnifiedIdeographs { get; }
            = ForRange(UnicodeRange.CJKUnifiedIdeographs, Localization.Get("OptionScriptCJKUnifiedIdeographs/Text"));

        public static BasicFontFilter KoreanHangul { get; }
            = ForRange(UnicodeRange.KoreanHangulSyllables, Localization.Get("OptionScriptKorean/Text"));

        public static BasicFontFilter ScriptBasicLatin { get; }
            = ForRange(UnicodeRange.BasicLatinLetters, Localization.Get("OptionScriptBasicLatin/Text"));


        public static List<BasicFontFilter> AllScriptsList { get; } = new List<BasicFontFilter>
        {
            ScriptArabic,
            ScriptCyrillic,
            ScriptGreekAndCoptic,
            ScriptHebrew,
            ScriptBasicLatin,
            ScriptThai,
            ScriptCJKUnifiedIdeographs
        };
    }
}
