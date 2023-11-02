namespace CharacterMap.Models;

public class SearchResultsGroup : List<IGlyphData>, IGrouping<string, IGlyphData>
{
    public string Key { get; }

    public SearchResultsGroup(string key, IEnumerable<IGlyphData> data) : base(data)
    {
        Key = key;
    }

    public static List<SearchResultsGroup> CreateGroups(IEnumerable<IGlyphData> items, List<UnicodeRangeModel> categories)
    {
        SearchResultsGroup active = new(null,
            items.Where(g => categories.Where(c => c.IsSelected).Any(c => c.Range.Contains((uint)g.UnicodeIndex))));

        SearchResultsGroup inactive = new("HIDDEN",
            items.Except(active));

        return new() { active, inactive };

    }

    public override string ToString() => Key;
}
