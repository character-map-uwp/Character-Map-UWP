﻿using Windows.Data.Text;

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
}
