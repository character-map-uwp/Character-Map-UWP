using SQLite;

namespace CharacterMap.Models
{
    public class AdobeGlyphListMapping
    {
        [Column("I1")]
        public int UnicodeIndex { get; set; }

        [Column("I2")]
        public int UnicodeIndex2 { get; set; }

        [Column("I3")]
        public int UnicodeIndex3 { get; set; }

        [Column("I4")]
        public int UnicodeIndex4 { get; set; }

        [PrimaryKey]
        [Column("S")]
        public string Value { get; set; }
    }

}
