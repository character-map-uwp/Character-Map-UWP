using Windows.Globalization.Collation;

namespace CharacterMap.Core;

public class AlphaKeyGroup<T> : ObservableCollection<T>
{
    private static readonly string[] AlphaKeys =
        ["#", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "&"];

    private static CharacterGroupings s_groupings;
    private static CharacterGroupings Groupings => s_groupings ??= new();

    public string Key { get; }

    public AlphaKeyGroup(string key)
    {
        Key = key;
    }

    public AlphaKeyGroup(string key, List<T> items) : base(items)
    {
        Key = key;
    }

    public static List<AlphaKeyGroup<T>> CreateGroups(IEnumerable<T> items, Func<T, string> keySelector)
    {
        CharacterGroupings slg = Groupings;
        Dictionary<string, List<T>> map = new(28, StringComparer.CurrentCulture);
        List<T> fallback = [];

        foreach (string key in AlphaKeys)
        {
            List<T> bucket = [];
            map[key] = bucket;
            if (key == "&")
                fallback = bucket;
        }

        foreach (T item in items)
        {
            string label = slg.Lookup(keySelector(item));
            if (map.TryGetValue(label, out List<T> bucket))
                bucket.Add(item);
            else
                fallback.Add(item);
        }

        List<AlphaKeyGroup<T>> result = new(28);
        foreach (string key in AlphaKeys)
            result.Add(new(key, map[key]));

        return result;
    }
}
