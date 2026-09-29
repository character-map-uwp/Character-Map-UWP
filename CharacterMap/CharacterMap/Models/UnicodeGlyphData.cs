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
    [MaxLength(5)]
    [Column("Hx")]
#endif
    public string UnicodeHex { get; set; }

    public string Description { get; set; }
}
