using CharacterMap.Core;
using CharacterMap.Models;
using CharacterMap.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Data.Text;

namespace CharacterMap.Helpers
{
    public static class Unicode
    {
        public static bool IsWhiteSpace(int c)
        {
            return ((c == ' ') || (c >= 0x0009 && c <= 0x000d) || c == 0x00a0 || c == 0x0085);
        }

        public static string GetHexValue(uint i) => (i <= 0x10FFFF && (i< 0xD800 || i> 0xDFFF)) ? char.ConvertFromUtf32((int) i) : new string ((char) i, 1);

        public static bool IsWhiteSpaceOrControl(uint c)
        {
            UnicodeGeneralCategory category = UnicodeCharacters.GetGeneralCategory(c);

            return category is UnicodeGeneralCategory.Control
                or UnicodeGeneralCategory.SpaceSeparator
                or UnicodeGeneralCategory.LineSeparator
                or UnicodeGeneralCategory.ParagraphSeparator;
        }

        public static bool IsInCategory(uint c, UnicodeGeneralCategory cat)
        {
            return cat == UnicodeCharacters.GetGeneralCategory(c);
        }

        public static bool ContainsRange(FontVariant v, UnicodeRange range)
        {
            return v.UnicodeRanges.Any(r => r.First <= range.End && range.Start <= r.Last);
        }

        public static bool SupportsScript(FontVariant v, UnicodeRange range)
        {
            // Filters out fonts that support just a singular symbol (like currency symbol)
            return v.UnicodeRanges.Any(r => r.First <= range.End && range.Start <= r.Last && ((r.Last - r.First) > 0));
        }

        public static bool ContainsEmoji(FontVariant v)
        {
            return ContainsRange(v, UnicodeRange.Emoticons)
                || ContainsRange(v, UnicodeRange.Dingbats)
                || ContainsEmojiSymbols(v);
        }

        public static bool ContainsEmojiSymbols(FontVariant v)
        {
            return ContainsRange(v, UnicodeRange.SymbolsExtended)
                || ContainsRange(v, UnicodeRange.MiscSymbols)
                || ContainsRange(v, UnicodeRange.SupplementalSymbols)
                || ContainsRange(v, UnicodeRange.TransportSymbols);
        }

        public static List<UnicodeCategoryModel> CreateCategoriesList(IList<UnicodeCategoryModel> source = null)
        {
            var list = Enum.GetValues(typeof(UnicodeGeneralCategory)).OfType<UnicodeGeneralCategory>().Select(e => new UnicodeCategoryModel(e)).ToList();

            if (source != null)
                for (int i = 0; i < list.Count; i++)
                    list[i].IsSelected = source[i].IsSelected;

            return list;
        }

        public static List<UnicodeRangeModel> CreateRangesList(IList<UnicodeRangeModel> source = null)
        {
            List<UnicodeRangeModel> list = source is null
                ? UnicodeRanges.All.Select(e => new UnicodeRangeModel(e)).ToList()
                : source.Select(s => s.Clone()).ToList();

            return list;
        }
    }
}
