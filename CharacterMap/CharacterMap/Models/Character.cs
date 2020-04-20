using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Models
{
    public class Character
    {
        public Character(uint unicodeIndex)
        {
            UnicodeIndex = unicodeIndex;
            Char = Unicode.GetHexValue(UnicodeIndex);
        }

        public string Char { get; }

        public uint UnicodeIndex { get; }

        public string UnicodeString => "U+" + UnicodeIndex.ToString("x4").ToUpper();

        public override string ToString()
        {
            return Char;
        }

        public string GetAnnotation(GlyphAnnotation a)
        {
            return a switch
            {
                GlyphAnnotation.None => string.Empty,
                GlyphAnnotation.UnicodeHex => UnicodeString,
                GlyphAnnotation.UnicodeIndex => UnicodeIndex.ToString(),
                _ => string.Empty
            };
        }
    }
}