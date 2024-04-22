namespace CharacterMap.Models;

public class Character : IEquatable<Character>, ICharacter
{
    public Character(uint unicodeIndex)
    {
        UnicodeIndex = unicodeIndex;
        Char = Unicode.GetHexValue(UnicodeIndex);
    }

    public string Char { get; }

    public uint UnicodeIndex { get; }

    public string UnicodeString => "U+" + UnicodeIndex.ToString("x4").ToUpper();

    private NamedUnicodeRange _range;
    public NamedUnicodeRange Range => _range ??= (UnicodeRanges.All.FirstOrDefault(r => r != UnicodeRanges.Unassigned && r.Contains(UnicodeIndex)) ?? UnicodeRanges.Unassigned);

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