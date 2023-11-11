namespace CharacterMap.Models;

public partial class BasicFontFilter
{
    public Func<IEnumerable<InstalledFont>, UserCollectionsService, IEnumerable<InstalledFont>> Query { get; }
    public string DisplayTitle { get; }
    public string FilterTitle { get; }

    /// <summary>
    /// Slower devices (like ARM devices) can lock up and be terminated by Windows when 
    /// filtering large font sets with expensive filters, like checking if a font has
    /// variable font axis (which requires creating an IDWriteFontFace for every font).
    /// This helps enable a quick workaround for these filters.
    /// </summary>
    public bool RequiresAsync { get; set; }

    public static BasicFontFilter ForDesignScriptTag(string tag, string displayTitle)
    {
        return new BasicFontFilter(
            (f, c) => f.Where(i => i.Variants.Any(v => v.HasDesignScriptTag(tag))),
            displayTitle,
            true);
    }

    public static BasicFontFilter ForNamedRange(NamedUnicodeRange range)
    {
        return new BasicFontFilter(
            (f, c) => f.Where(i => i.Variants.Any(v => Unicode.ContainsRange(v, range.Range))),
            range.Name,
            true);
    }

    public static BasicFontFilter ForRange(UnicodeRange range, string displayTitle)
    {
        return new BasicFontFilter((f, c) => f.Where(v => v.Variants.Any(v => Unicode.ContainsRange(v, range))), displayTitle);
    }

    public static BasicFontFilter SupportsScript(UnicodeRange range, string displayTitle)
    {
        return new BasicFontFilter((f, c) => f.Where(v => v.Variants.Any(v => Unicode.SupportsScript(v, range))), displayTitle);
    }

    public static BasicFontFilter SupportsScripts(UnicodeRange[] ranges, string displayTitle)
    {
        return new BasicFontFilter((f, c) => f.Where(v => v.Variants.Any(v => ranges.Any(r => Unicode.SupportsScript(v, r)))), displayTitle);
    }

    public BasicFontFilter(Func<IEnumerable<InstalledFont>, UserCollectionsService, IEnumerable<InstalledFont>> query, string displayTitle, bool requiresAsync = false)
    {
        Query = query;
        DisplayTitle = displayTitle;
        FilterTitle = displayTitle;
        RequiresAsync = requiresAsync;
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
        = new((f, c) => f, Localization.Get("OptionAllFonts/Text"));

    public static BasicFontFilter SymbolFonts { get; }
        = new((f, c) => f.Where(v => v.IsSymbolFont || c.SymbolCollection.Fonts.Contains(v.Name)), Localization.Get("OptionSymbolFonts/Text"));

    public static BasicFontFilter ImportedFonts { get; }
        = new((f, c) => f.Where(v => v.HasImportedFiles), Localization.Get("OptionImportedFonts/Text"));

    public static BasicFontFilter MonospacedFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.IsMonospacedFont), Localization.Get("OptionMonospacedFonts/Text"));

    public static BasicFontFilter SerifFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.Panose.IsSerifStyle), Localization.Get("OptionSerifFonts/Text"));

    public static BasicFontFilter SansSerifFonts { get; }
       = new((f, c) => f.Where(v => v.DefaultVariant.Panose.IsSansSerifStyle), Localization.Get("OptionSansSerifFonts/Text"));

    public static BasicFontFilter AppXFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.Source == DWriteFontSource.AppxPackage), Localization.Get("OptionAppxFonts/Text"));

    public static BasicFontFilter RemoteFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.Source == DWriteFontSource.RemoteFontProvider), Localization.Get("OptionCloudFonts/Text"));

    public static BasicFontFilter PanoseDecorativeFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.Panose.Family == PanoseFamily.Decorative), Localization.Get("OptionDecorativeFonts/Text"));

    public static BasicFontFilter PanoseScriptFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.Panose.Family == PanoseFamily.Script), Localization.Get("OptionScriptFonts/Text"));

    public static BasicFontFilter ColorFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.IsColorFont), Localization.Get("OptionColorFonts/Text"));

    public static BasicFontFilter VariableFonts { get; }
        = new((f, c) => f.Where(v => v.DefaultVariant.DirectWriteProperties.HasVariations), Localization.Get("OptionVariableFonts/Text"), true);




    /* EMOJI */

    public static BasicFontFilter EmojiAll { get; }
        = new(
                (f, c) => f.Where(v => v.Variants.Any(v => Unicode.ContainsEmoji(v))),
                Localization.Get("OptionAllEmoji/Text"),
                Localization.Get("OptionAllEmojiTitle/Text"));

    public static BasicFontFilter EmojiEmoticons { get; }
        = ForRange(UnicodeRange.Emoticons, Localization.Get("OptionEmojiEmoticons/Text"));

    public static BasicFontFilter EmojiDingbats { get; }
        = ForRange(UnicodeRange.Dingbats, Localization.Get("OptionEmojiDingbats/Text"));

    public static BasicFontFilter EmojiSymbols { get; }
        = new(
                (f, c) => f.Where(v => v.Variants.Any(v => Unicode.ContainsEmojiSymbols(v))),
                Localization.Get("OptionEmojiSymbols/Text"));




    /* SCRIPTS */

    public static BasicFontFilter ScriptArabic { get; }
        = SupportsScript(UnicodeRanges.Arabic.Range, Localization.Get("OptionScriptArabic/Text"));

    public static BasicFontFilter ScriptBengali { get; }
        = SupportsScript(UnicodeRanges.Bengali.Range, Localization.Get("OptionsScriptBengali/Text"));

    public static BasicFontFilter ScriptCyrillic { get; }
        = SupportsScript(UnicodeRanges.Cyrillic.Range, Localization.Get("OptionScriptCyrillic/Text"));

    public static BasicFontFilter ScriptGreekAndCoptic { get; }
        = SupportsScript(UnicodeRange.GreekAndCoptic, Localization.Get("OptionScriptGreekAndCoptic/Text"));

    public static BasicFontFilter ScriptDevanagari { get; }
        = SupportsScript(UnicodeRanges.Devanagari.Range, Localization.Get("OptionsScriptDevanagari/Text"));

    public static BasicFontFilter ScriptHiraganaAndKatakana { get; }
        = SupportsScripts(new[] { UnicodeRanges.Hiragana.Range, UnicodeRanges.Katakana.Range }, Localization.Get("OptionScriptHiraganaAndKatakana/Text"));

    public static BasicFontFilter ScriptHebrew { get; }
        = ForRange(UnicodeRange.Hebrew, Localization.Get("OptionScriptHebrew/Text"));

    public static BasicFontFilter ScriptThai { get; }
        = SupportsScript(UnicodeRange.Thai, Localization.Get("OptionScriptThai/Text"));

    public static BasicFontFilter ScriptCJKUnifiedIdeographs { get; }
        = ForRange(UnicodeRange.CJKUnifiedIdeographs, Localization.Get("OptionScriptCJKUnifiedIdeographs/Text"));

    public static BasicFontFilter ScriptKoreanHangul { get; }
        = ForRange(UnicodeRange.KoreanHangulSyllables, Localization.Get("OptionScriptKorean/Text"));

    public static BasicFontFilter ScriptBasicLatin { get; }
        = ForRange(UnicodeRange.BasicLatinLetters, Localization.Get("OptionScriptBasicLatin/Text"));


    public static List<BasicFontFilter> AllScriptsList { get; } = new()
    {
        ScriptArabic,
        ScriptCyrillic,
        ScriptGreekAndCoptic,
        ScriptHebrew,
        ScriptBasicLatin,
        ScriptThai,
        ScriptCJKUnifiedIdeographs,
        ScriptKoreanHangul,
    };
}
