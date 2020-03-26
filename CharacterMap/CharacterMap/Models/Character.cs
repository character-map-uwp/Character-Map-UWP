namespace CharacterMap.Models
{
    public class Character
    {
        public string Char { get; set; }

        public int UnicodeIndex { get; set; }

        public string UnicodeString => "U+" + UnicodeIndex.ToString("x4").ToUpper();

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