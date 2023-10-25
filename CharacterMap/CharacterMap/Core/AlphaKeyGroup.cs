using System.Collections.ObjectModel;
using Windows.Globalization.Collation;

namespace CharacterMap.Core;

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
                    if (i.UnicodeIndex >= 0xE000 && i.UnicodeIndex <= 0xE5FF)
                        return UnicodeRanges.MDL2Deprecated;
                    else if (i.UnicodeIndex >= 0xE600)
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

public class AlphaKeyGroup<T> : ObservableCollection<T>
{
    public string Key { get; private set; }

    public AlphaKeyGroup(string key)
    {
        Key = key;
    }

    // Work around for Chinese version of Windows
    // By default, Chinese language group will create useless "拼音A-Z" groups.
    private static List<AlphaKeyGroup<T>> CreateAZGroups()
    {
        char[] alpha = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ&".ToCharArray();
        var list = alpha.Select(c => new AlphaKeyGroup<T>(c.ToString())).ToList();
        return list;
    }

    public static List<AlphaKeyGroup<T>> CreateGroups(IEnumerable<T> items, Func<T, string> keySelector)
    {
        CharacterGroupings slg = new ();
        List<AlphaKeyGroup<T>> list = CreateAZGroups(); //CreateDefaultGroups(slg);
        foreach (T item in items)
        {
            int index = 0;
            string label = slg.Lookup(keySelector(item));
            index = list.FindIndex(alphagroupkey => (alphagroupkey.Key.Equals(label, StringComparison.CurrentCulture)));
            if (index > -1 && index < list.Count)
                list[index].Add(item);
            else
                list.Last().Add(item);
        }
        return list;
    }
}
