namespace CharacterMap.Models;


[DebuggerDisplay("{Name}, Start: {Start}, End: {End}")]
public record class NamedUnicodeRange
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

    public virtual bool Contains(uint index) => index >= Start && index <= End;
}

public sealed record class UnassignedUnicodeRange : NamedUnicodeRange
{
    public UnassignedUnicodeRange() : base("Unassigned", 0x110000, 200000) { }

    public override bool Contains(uint index) => UnicodeRanges.GetRange(index) == this;
}
