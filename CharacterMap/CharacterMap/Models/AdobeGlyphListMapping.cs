using SQLite;

namespace CharacterMap.Models;

public class AdobeGlyphListMapping
{
#if DEBUG
    [Column("I1")]
#endif
    public int UnicodeIndex { get; set; }

#if DEBUG
    [Column("I2")]
#endif
    public int UnicodeIndex2 { get; set; }

#if DEBUG
    [Column("I3")]
#endif
    public int UnicodeIndex3 { get; set; }

#if DEBUG
    [Column("I4")]
#endif
    public int UnicodeIndex4 { get; set; }

#if DEBUG
    [PrimaryKey]
    [Column("S")]
#endif
    public string Value { get; set; }
}
