namespace CharacterMap.Models;

public partial class Character : IEquatable<Character>
{
    public uint UnicodeIndex { get; } = uint.MaxValue;

    public Character(uint unicodeIndex) => UnicodeIndex = unicodeIndex;

    public bool IsValidUnicode => UnicodeIndex != uint.MaxValue;

    public string Char => Unicode.GetChar(UnicodeIndex);

    public string UnicodeString => "U+" + UnicodeIndex.ToString("x4").ToUpper();

    public bool CouldBeUnihan => Unicode.CouldBeUnihan(UnicodeIndex);

    public NamedUnicodeRange Range => UnicodeRanges.GetRange(UnicodeIndex);

    public override string ToString() => Char;


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

    public string GetClipboardString()
    {
        // Check if SurrogatePair
        if (UnicodeIndex >= 0x010000 && UnicodeIndex <= 0x10FFFF)
        {
            Windows.Data.Text.UnicodeCharacters.GetSurrogatePairFromCodepoint(UnicodeIndex, out char high, out char low);
            return @$"\u{(uint)high}?\u{(uint)low}?";
        }
        else
            return @$"\u{UnicodeIndex}?";
    }






    /* Using a tiered character cache avoids a lot of unnecessary allocations */
    private static Character[] _bmpCharacters { get; } = new Character[65536];

    /* For large fonts we use a small rotating cache, to prevent caching potentially MILLIONS of glyph */
    private static readonly Character[] _supplementaryCache = new Character[2048];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Character Get(int i)
    {
        if ((uint)i < 65536)
        {
            Character c = _bmpCharacters[i];
            if (c is null)
                _bmpCharacters[i] = c = new((uint)i);
            return c;
        }

        //lock (_supplementaryCharacters)
        //{
        /* 
           This rotating cache MIGHT generate a lot of collisions in rare circumstances because it assumes in-order
           display of contiguous ranges in a single view, which will not always be the case.
           If it fails, nothing explodes but we will be constantly allocating new objects.
           Oh well.
        */

        int slot = (int)((uint)i & 2047);
        Character cached = _supplementaryCache[slot];
        if (cached is not null && cached.UnicodeIndex == (uint)i)
            return cached;

        Character created = new((uint)i);
        _supplementaryCache[slot] = created;
        return created;
        //}
    }


    #region Equality

    public override bool Equals(object obj) => Equals(obj as Character);

    public bool Equals(Character other) => other != null && UnicodeIndex == other.UnicodeIndex;

    public override int GetHashCode() => 1044413180 + UnicodeIndex.GetHashCode();

    public static bool operator ==(Character left, Character right) => EqualityComparer<Character>.Default.Equals(left, right);

    public static bool operator !=(Character left, Character right) => !(left == right);

    #endregion
}

public class SpecialCharacters
{
    public static Character Null => field ??= Character.Get(0);
    public static Character CarriageReturn => field ??= Character.Get(13);
    public static Character Space => field ??= Character.Get(0x0020);

    public static Character NonBreakingSpace => field ??= Character.Get(0x00A0);
    public static Character ZeroWidthSpace => field ??= Character.Get(0x200B);
    public static Character ZeroWidthNonJoiner => field ??= Character.Get(0x200C);
    public static Character ZeroWidthJoiner => field ??= Character.Get(0x200D);

    public static Character INVALID => field ??= new(uint.MaxValue);
}