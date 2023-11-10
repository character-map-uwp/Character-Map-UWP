namespace CharacterMap.Models;

public partial class SearchTarget
{
    public Type TargetType;
    public string SearchTable;

    private Func<FontVariant, bool> _isTarget;

    public SearchTarget(Type t, string table, Func<FontVariant, bool> isTarget)
    {
        TargetType = t;
        SearchTable = table;
        _isTarget = isTarget;
    }

    public bool IsTarget(FontVariant variant) => _isTarget(variant);
}



// Static Members for Search Target
public partial class SearchTarget
{
    public static SearchTarget FontAwesomeTarget { get; }
        = new SearchTarget(
            typeof(FontAwesomeGlyph),
            SQLiteGlyphProvider.FONTAWESOME_SEARCH_TABLE,
            v => v.FamilyName.StartsWith("Font Awesome"));

    public static SearchTarget WebdingsTarget { get; }
        = new SearchTarget(
            typeof(WebdingsGlyph),
            SQLiteGlyphProvider.WEBDINGS_SEARCH_TABLE,
            v => v.FamilyName == "Webdings");

    public static SearchTarget WingdingsTarget { get; }
        = new SearchTarget(
            typeof(WingdingsGlyph),
            SQLiteGlyphProvider.WINGDINGS_SEARCH_TABLE,
            v => v.FamilyName == "Wingdings");

    public static SearchTarget Wingdings2Target { get; }
        = new SearchTarget(
            typeof(Wingdings2Glyph),
            SQLiteGlyphProvider.WINGDINGS2_SEARCH_TABLE,
            v => v.FamilyName == "Wingdings 2");

    public static SearchTarget Wingdings3Target { get; }
        = new SearchTarget(
            typeof(Wingdings3Glyph),
            SQLiteGlyphProvider.WINGDINGS3_SEARCH_TABLE,
            v => v.FamilyName == "Wingdings 3");

    public static List<SearchTarget> KnownTargets { get; } = new()
    {
        FontAwesomeTarget,
        WebdingsTarget,
        WingdingsTarget,
        Wingdings2Target,
        Wingdings3Target
    };
}
