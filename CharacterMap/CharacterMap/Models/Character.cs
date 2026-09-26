namespace CharacterMap.Models;

public partial class Character : IEquatable<Character>
{
    public uint UnicodeIndex { get; }

    public Character(uint unicodeIndex) => UnicodeIndex = unicodeIndex;

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
    public static Character Null => field ??= new(0);
    public static Character CarriageReturn => field ??= new(13);
    public static Character Space => field ??= new(0x0020);

    public static Character NonBreakingSpace => field ??= new(0x00A0);
    public static Character ZeroWidthSpace => field ??= new(0x200B);
    public static Character ZeroWidthNonJoiner => field ??= new(0x200C);
    public static Character ZeroWidthJoiner => field ??= new(0x200D);
}