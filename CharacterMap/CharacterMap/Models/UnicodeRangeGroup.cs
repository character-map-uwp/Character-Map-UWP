namespace CharacterMap.Models;

public class UnicodeRangeGroup : List<Character>, IGrouping<NamedUnicodeRange, Character>
{
    public NamedUnicodeRange Key { get; private set; }

    public UnicodeRangeGroup(NamedUnicodeRange key, IEnumerable<Character> items) : base(items)
    {
        Key = key;
    }

    public static ObservableCollection<UnicodeRangeGroup> CreateGroups(IEnumerable<Character> items, bool mdl2 = false)
    {
        if (!mdl2)
        {
            return new(items
                .GroupBy(i => i.Range ?? throw new InvalidOperationException(), c => c)
                .Select(g => new UnicodeRangeGroup(g.Key, g)));
        }
        else
        {
            return new(items
                .GroupBy(i =>
                {
                    if (UnicodeRanges.MDL2Deprecated.Contains(i.UnicodeIndex))
                        return UnicodeRanges.MDL2Deprecated;
                    else if (UnicodeRanges.PrivateUseAreaMDL2.Contains(i.UnicodeIndex))
                        return UnicodeRanges.PrivateUseAreaMDL2;
                    else
                        return i.Range ?? throw new InvalidOperationException();
                }, c => c)
                .Select(g => new UnicodeRangeGroup(g.Key, g)));
        }

    }

    public override string ToString()
    {
        return Key.Name;
    }
}
