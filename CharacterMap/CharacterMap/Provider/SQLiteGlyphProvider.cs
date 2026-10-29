using CharacterMapCX;
using SQLite;
using System.Globalization;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider;

public interface IFaceSearchSource
{
    Dictionary<Character, String> SearchMap { get; }
    CMFontFace Face { get; }
}

public partial class SQLiteGlyphProvider : IGlyphDataProvider
{
    /* Used for FTS searches */
    internal const string FONTAWESOME_SEARCH_TABLE = "fontawesomesearch";
    internal const string MDL2_SEARCH_TABLE = "mdl2search";
    internal const string UNICODE_SEARCH_TABLE = "unicodesearch";
    internal const string WEBDINGS_SEARCH_TABLE = "wbdsearch";
    internal const string WINGDINGS_SEARCH_TABLE = "wngsearch";
    internal const string WINGDINGS2_SEARCH_TABLE = "wng2search";
    internal const string WINGDINGS3_SEARCH_TABLE = "wng3search";

    private int SEARCH_LIMIT => ResourceHelper.AppSettings.MaxSearchResult;

    private SQLiteConnection _connection { get; set; }

    public void Initialise()
    {
        string path = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Data", "GlyphData.db");
        _connection = new(new SQLiteConnectionString(path, SQLiteOpenFlags.ReadOnly, true));
        _connection.ExecuteScalarStr("PRAGMA mmap_size = 8388608;");
        _connection.Execute("PRAGMA query_only = 1;");
    }

#if DEBUG && GENERATE_DATABASE
    public Task InitialiseDatabaseAsync()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        /* 
         * If you update the dataset and wish to generate a NEW dataset
         * run the code below
         */

        return InitialiseDebugAsync();
    }
#endif

    public string GetAdobeGlyphListMapping(string postscriptName)
    {
        // Adobe glyph list names don't have spaces in them so we can use this as a quick check to get out of here
        if (!postscriptName.Contains(" "))
        {
            // We perform only very naive AGFLN mappings here. We only identify the basic cases.
            if (_connection.GetGlyphListMapping(postscriptName) is { } mapping)
            {
                var desc = _connection.GetUnicodeDescription(mapping.UnicodeIndex);
                if (mapping.UnicodeIndex2 > 0)
                    desc += " " + _connection.GetUnicodeDescription(mapping.UnicodeIndex2);
                if (mapping.UnicodeIndex3 > 0)
                    desc += " " + _connection.GetUnicodeDescription(mapping.UnicodeIndex3);
                if (mapping.UnicodeIndex4 > 0)
                    desc += " " + _connection.GetUnicodeDescription(mapping.UnicodeIndex4);

                if (!string.IsNullOrWhiteSpace(desc))
                    return desc;
            }
        }

        return postscriptName;
    }

    public List<UnihanReading> GetUnihanReadings(int unicodeIndex)
    {
        return _connection.GetUnihanReadings(unicodeIndex);
    }


    public string GetCharacterDescription(int unicodeIndex, CMFontFace variant)
    {
        string desc = null;

        // MDL2 has it's own special logic
        if (FontFinder.IsMDL2(variant))
        {
            desc = _connection.GetUnicodeDescription(unicodeIndex, nameof(MDL2Glyph));
            if (string.IsNullOrWhiteSpace(desc) && Enum.IsDefined(typeof(Symbol), unicodeIndex))
                return ((Symbol)unicodeIndex).Humanise();
            return desc;
        }

        // Otherwise check if we have a search table for this font
        foreach (var target in SearchTarget.KnownTargets)
        {
            if (target.IsTarget(variant))
            {
#if USE_FTS
                desc = _connection.GetUnicodeDescription(unicodeIndex, target.SearchTable);
#else
                desc = _connection.GetUnicodeDescription(unicodeIndex, target.TargetType.Name);
#endif
                break;
            }
        }

        // Otherwise get a fallback value
        if (string.IsNullOrEmpty(desc))
            desc = _connection.GetUnicodeDescription(unicodeIndex);

        return desc;
    }




    #region SEARCH

    public Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, IFaceSearchSource variant)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(GlyphService.EMPTY_SEARCH);

        /* MDL2 has special dataset */
        if (FontFinder.IsMDL2(variant.Face))
            return SearchMDL2Async(query, variant);

        foreach (SearchTarget target in SearchTarget.KnownTargets)
        {
            if (target.IsTarget(variant.Face))
                return InternalSearchAsync(target.SearchTable, target.TargetType.Name, query, variant);
        }

        /* Generic Unicode Search */
        return SearchUnicodeAsync(query, variant);
    }

    private Task<IReadOnlyList<IGlyphData>> SearchUnicodeAsync(string query, IFaceSearchSource variant)
    {
        return InternalSearchAsync(UNICODE_SEARCH_TABLE, nameof(UnicodeGlyphData), query, variant);
    }

    private Task<IReadOnlyList<IGlyphData>> SearchMDL2Async(string query, IFaceSearchSource variant)
    {
        return InternalSearchAsync(MDL2_SEARCH_TABLE, nameof(MDL2Glyph), query, variant);
    }

    private Task<IReadOnlyList<IGlyphData>> InternalSearchAsync(string ftsTable, string table, string query, IFaceSearchSource variant)
    {
        return Task.Run<IReadOnlyList<IGlyphData>>(() =>
        {
            /* 
             * Step 1: Perform single-result Hex Search if hex
             * Step 2: Perform local SearchMap search if present
             * Step 3: Perform streaming LIKE search filtered by binary search over UnicodeRanges
             */

            DWriteUnicodeRange[] ranges = variant.Face.UnicodeRanges;
            if (ranges.Length == 0)
                return GlyphService.EMPTY_SEARCH;

            // 1. Decide if hex or text search
            //    If hex, search the main table (UnicodeIndex column is indexed)
            GlyphDescription hexResult = null;

            // 1.1 If single Char, try forcing as hex search
            if (query.Length == 1)
                query = ((uint)query[0]).ToString("x4");

            bool ambiguous = !variant.Face.DirectWriteProperties.IsSymbolFont && IsAmbiguousQuery(query);
            if (hexResult == null && Utils.TryParseHexString(query, out int hex))
            {
                // 1.2. First check if the font actually contains the UnicodeIndex via binary search.
                //      If it does then we ask the database, otherwise we can skip querying.
                if (SQLite3Extensions.ContainsCodepoint(ranges, (uint)hex))
                {
                    string hexsql = $"SELECT * FROM {table} WHERE Ix == {hex} LIMIT 1";
                    List<GlyphDescription> hexresults = _connection.GetGlyphData(table, hexsql, query);
                    if (hexresults == null || hexresults.Count == 0)
                    {
                        string label = hex.ToString("x4");
                        hexresults =
                        [
                            new GlyphDescription
                            {
                                UnicodeIndex = hex,
                                UnicodeHex = label,
                                Description = label
                            }
                        ];
                    }

                    hexResult = hexresults[0];
                }
            }

            List<IGlyphData> results = [];
            int searchLimit = SEARCH_LIMIT;

            // 2. If the font has a local search map, we should do a text search inside that
            if (variant.SearchMap != null)
            {
                foreach (KeyValuePair<Character, string> kvp in variant.SearchMap)
                {
                    if (kvp.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new GlyphDescription
                        {
                            UnicodeIndex = (int)kvp.Key.UnicodeIndex,
                            UnicodeHex = kvp.Key.UnicodeString,
                            Description = kvp.Value
                        });

                        if (results.Count >= searchLimit)
                            goto End;
                    }
                }
            }

            // 3. Stream matching rows from the main table and filter candidate Ix values in C#
            //    using binary search over variant.Face.UnicodeRanges instead of building a massive
            //    990-branch WHERE (Ix BETWEEN ...) SQL expression tree on every keystroke.
            string likeQuery = $"%{query}%";
            _connection.SearchGlyphDescriptions(table, ranges, likeQuery, searchLimit, results);

            if (results.Count >= searchLimit)
                goto End;

            // 4. Check Unihan data
            if (variant.Face.CouldContainUnihan())
                _connection.SearchUnihanDefinitions(ranges, likeQuery, searchLimit, results);

        End:
            if (hexResult is not null)
                results.Insert(0, hexResult);
            return results;
        });
    }

    bool IsAmbiguousQuery(string s)
    {
        // We need to check the possibility this could be either HEX or NAME search.
        // For example, "ed" could return hex or a partial text result
        return s.Length <= 7 && !s.Any(c => char.IsLetter(c) == false);
    }

    #endregion
}
