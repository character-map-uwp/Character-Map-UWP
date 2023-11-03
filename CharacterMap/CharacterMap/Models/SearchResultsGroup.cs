namespace CharacterMap.Models;

public class SearchResultsGroups : List<SearchResultsGroup>
{
    public bool HasHiddenResults { get; }

    public SearchResultsGroups(params SearchResultsGroup[]  groups) : base(groups)
    {
        HasHiddenResults = groups.Length > 1 && groups[1].Count > 0;
    }
}

public class SearchResultsGroup : List<IGlyphData>, IGrouping<string, IGlyphData>
{
    public string Key { get; }

    public SearchResultsGroup(string key, IEnumerable<IGlyphData> data) : base(data)
    {
        Key = key;
    }

    public static SearchResultsGroups CreateGroups(IEnumerable<IGlyphData> items, List<UnicodeRangeModel> categories)
    {
        SearchResultsGroup active = new(null,
            items.Where(g => categories.Where(c => c.IsSelected).Any(c => c.Range.Contains((uint)g.UnicodeIndex))));

        SearchResultsGroup inactive = new(
            Localization.Get("HiddenSearchResultsLabel/Text"),
            items.Except(active));

        return new(active, inactive);
    }

    public override string ToString() => Key;
}
