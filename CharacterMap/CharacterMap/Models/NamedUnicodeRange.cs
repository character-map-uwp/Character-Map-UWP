namespace CharacterMap.Models;

[DebuggerDisplay("{Name}, Start: {Start}, End: {End}")]
public class NamedUnicodeRange
{
    public string Name { get; }
    public uint Start { get; }
    public uint End { get; }

    public UnicodeRange Range { get; }

    public NamedUnicodeRange(string name, uint start, uint length)
    {
        Name = name;
        Start = start;
        End = start + length - 1;
        Range = new(Start, End);
    }

    /* Constructor with a defined "END" value */
    public NamedUnicodeRange(uint start, uint end, string name)
    {
        Name = name;
        Start = start;
        End = end;
        Range = new(Start, End);
    }

    public bool Contains(uint index) => index >= Start && index <= End;
}
