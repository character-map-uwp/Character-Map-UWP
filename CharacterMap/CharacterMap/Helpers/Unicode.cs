using Windows.Data.Text;

namespace CharacterMap.Helpers;

public static class Unicode
{
    public const uint UNIHAN_IDX = 0x3000;

    public static bool IsWhiteSpace(int c)
    {
        return ((c == ' ') || (c >= 0x0009 && c <= 0x000d) || c == 0x00a0 || c == 0x0085);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CouldBeUnihan(uint index) => index >= UNIHAN_IDX;

    private static readonly string[] _latinCache = [.. Enumerable.Range(0, 256).Select(i => GetHexValue((uint)i))];
    public static string GetChar(uint i) => i < 256 ? _latinCache[i] : GetHexValue(i);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetHexValue(uint i) => (i <= 0x10FFFF && (i < 0xD800 || i > 0xDFFF)) ? char.ConvertFromUtf32((int)i) : new string((char)i, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RequiresSurrogates(Character c) => c.UnicodeIndex >= 0x010000;

    public static bool IsWhiteSpaceOrControl(uint c)
    {
        UnicodeGeneralCategory category = UnicodeCharacters.GetGeneralCategory(c);

        return category is UnicodeGeneralCategory.Control
            or UnicodeGeneralCategory.SpaceSeparator
            or UnicodeGeneralCategory.LineSeparator
            or UnicodeGeneralCategory.ParagraphSeparator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsRange(CMFontFace v, UnicodeRange range)
    {
        return v.UnicodeRanges.Any(r => r.First <= range.End && range.Start <= r.Last);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SupportsScript(CMFontFace v, UnicodeRange range)
    {
        // Filters out fonts that support less than two glyphs in the script range
        return v.UnicodeRanges.Any(r => r.First <= range.End && range.Start <= r.Last && ((r.Last - r.First) > 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsEmoji(CMFontFace v)
    {
        return ContainsRange(v, UnicodeRange.Emoticons)
            || ContainsRange(v, UnicodeRange.Dingbats)
            || ContainsEmojiSymbols(v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsEmojiSymbols(CMFontFace v)
    {
        return ContainsRange(v, UnicodeRange.SymbolsExtended)
            || ContainsRange(v, UnicodeRange.MiscSymbols)
            || ContainsRange(v, UnicodeRange.SupplementalSymbols)
            || ContainsRange(v, UnicodeRange.TransportSymbols);
    }

    public static List<UnicodeRangeModel> CreateRangesList(IList<UnicodeRangeModel> source = null)
    {
        List<UnicodeRangeModel> list = source is null
            ? UnicodeRanges.All.Select(e => new UnicodeRangeModel(e)).ToList()
            : source.Select(s => s.Clone()).ToList();

        return list;
    }

    public static List<Character> FilterCharacters(IReadOnlyList<Character> characters, IList<UnicodeRangeModel> categories, bool hideWhitespace)
    {
        var chars = characters.AsEnumerable();

        if (hideWhitespace)
            chars = chars.Where(c => !Unicode.IsWhiteSpaceOrControl(c.UnicodeIndex));

        foreach (var cat in categories.Where(c => !c.IsSelected))
            chars = chars.Where(c => !cat.Range.Contains(c.UnicodeIndex));

        return chars.ToList();
    }

    public static List<UnicodeRangeModel> GetCategories(CMFontFace variant, bool mdl2)
    {
        var ranges = variant.GetRanges();
        var cats = UnicodeRanges.All
            .Where(r => ranges.Any(g => g.Name == r.Name))
            .Select(r => new UnicodeRangeModel(r))
            .ToList();

        if (mdl2)
        {
            if (cats.FirstOrDefault(m => m.Range == UnicodeRanges.PrivateUseArea) is { } pua)
            {
                int idx = cats.IndexOf(pua);
                cats.RemoveAt(idx);
                cats.Insert(idx, new UnicodeRangeModel(UnicodeRanges.PrivateUseAreaMDL2));
                cats.Insert(idx, new UnicodeRangeModel(UnicodeRanges.MDL2Deprecated) { IsSelected = !ResourceHelper.AppSettings.HideDeprecatedMDL2 });
            }
            else
            {
                cats.Add(new UnicodeRangeModel(UnicodeRanges.MDL2Deprecated) { IsSelected = !ResourceHelper.AppSettings.HideDeprecatedMDL2 });
                cats.Add(new UnicodeRangeModel(UnicodeRanges.PrivateUseAreaMDL2));
            }
        }

        return cats;
    }

    /// <summary>
    /// Many "ligatures" defined in a font are not what a consumer would typically consider a ligature
    /// (like Emoji variations). This method attempts to filter those out.
    /// </summary>
    /// <param name="sequence"></param>
    /// <returns></returns>
    public static bool IsCharacterLigature(string sequence)
    {
        /*
         * This method:
         *   - Excludes Emojis and Variations by rejecting any sequence containing ZWJ (U+200D),
         *     variation selectors (U+FE0F, U+FE0E), and surrogate unicode ranges where emojis reside
         *   - Allows 'Standard Letters' from all alphabets (Latin, Cyrillic, Greek, Arabic, etc.)
         *   - Allows Basic Programming Symbols like standard mathematical operators and punctuation symbols
         *     (e.g. <=, !=, ===) that constitute programming ligatures
         */

        if (string.IsNullOrEmpty(sequence) || sequence.Length < 2)
            return false;

        foreach (char c in sequence)
        {
            if (char.IsControl(c) || c == '\u200D' || c == '\uFE0F' || c == '\uFE0E')
                return false;

            if (char.IsLetter(c))
                continue;

            if (c < 128 && (char.IsPunctuation(c) || char.IsSymbol(c)))
                continue;

            return false;
        }

        return true;
    }


    public static bool IsZWJ(uint i) => i == 0x200D;
    public static bool IsZWJ(string c) => c == "\u200D";

    public static bool IsZWNJ(uint i) => i == 0x200C;
    public static bool IsZWNJ(string c) => c == "\u200C";

    public static bool IsSpecial(uint? i) => i is uint u && IsSpecial(u);
    public static bool IsSpecial(uint i)
    {
        return i
            is 0x200C // ZWNJ
            or 0x200D // ZWJ
            or 0xFE0F // VS16
            or 0xFE0E // VS15
            or 0x00A0 // NBSP
            or 0x0020; // Space
    }
}
