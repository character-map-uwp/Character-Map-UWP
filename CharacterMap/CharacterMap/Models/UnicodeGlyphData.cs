using CharacterMap.Services;
using SQLite;

namespace CharacterMap.Models
{
    public class UnicodeGlyphData : IGlyphData
    {
        [PrimaryKey]
        [Column("Ix")]
        public int UnicodeIndex { get; set; }

        [Indexed]
        [MaxLength(5)]
        [Column("Hx")]
        public string UnicodeHex { get; set; }

        [Indexed]
        [MaxLength(2)]
        public string UnicodeGroup { get; set;}

        public string Description { get; set; }
    }

}
