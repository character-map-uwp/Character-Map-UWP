#if DEBUG
using SQLite;
#endif

namespace CharacterMap.Models;


public class UnicodeGlyphData : IGlyphData
{
#if DEBUG
    [PrimaryKey]
    [Column("Ix")]
#endif
    public int UnicodeIndex { get; set; }

#if DEBUG
    [Indexed]
    [MaxLength(5)]
    [Column("Hx")]
#endif
    public string UnicodeHex { get; set; }

#if DEBUG
    [Indexed]
    [MaxLength(2)]
#endif
    public string UnicodeGroup { get; set;}

    public string Description { get; set; }
}
