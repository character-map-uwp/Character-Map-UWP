namespace CharacterMap.Models;

public record FontLigature(ushort GlyphIndex, string Sequence);

public class Character : IEquatable<Character>
{
    public static Character Null => field ??= new(0);
    public static Character CarriageReturn => field ??= new(13);
    public static Character Space => field ??= new(32);




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




    public override bool Equals(object obj)
    {
        return Equals(obj as Character);
    }

    public bool Equals(Character other)
    {
        return other != null &&
               UnicodeIndex == other.UnicodeIndex;
    }

    public override int GetHashCode()
    {
        return 1044413180 + UnicodeIndex.GetHashCode();
    }

    public static bool operator ==(Character left, Character right)
    {
        return EqualityComparer<Character>.Default.Equals(left, right);
    }

    public static bool operator !=(Character left, Character right)
    {
        return !(left == right);
    }
}