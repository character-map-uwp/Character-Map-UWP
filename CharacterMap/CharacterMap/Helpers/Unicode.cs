using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Text;

namespace CharacterMap.Helpers
{
    public static class Unicode
    {
        public static bool IsWhiteSpace(int c)
        {
            return ((c == ' ') || (c >= 0x0009 && c <= 0x000d) || c == 0x00a0 || c == 0x0085);
        }

        public static string GetHexValue(int i) => (i <= 0x10FFFF && (i< 0xD800 || i> 0xDFFF)) ? char.ConvertFromUtf32((int) i) : new string ((char) i, 1);

        public static bool IsWhiteSpaceOrControl(int c)
        {
            UnicodeGeneralCategory category = UnicodeCharacters.GetGeneralCategory((uint)c);

            return category == UnicodeGeneralCategory.Control
                || category == UnicodeGeneralCategory.SpaceSeparator
                || category == UnicodeGeneralCategory.LineSeparator
                || category == UnicodeGeneralCategory.ParagraphSeparator;
        }

        public static bool IsInCategory(int c, UnicodeGeneralCategory cat)
        {
            return cat == UnicodeCharacters.GetGeneralCategory((uint)c);
        }
    }
}
