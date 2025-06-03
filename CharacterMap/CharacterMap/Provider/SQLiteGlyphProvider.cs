﻿using SQLite;
using System.Globalization;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider;

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
        var path = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Data", "GlyphData.db");
        _connection = new SQLiteConnection(new SQLiteConnectionString(path, SQLiteOpenFlags.ReadOnly, true));
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

    public Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, CMFontFace variant)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(GlyphService.EMPTY_SEARCH);

        /* MDL2 has special dataset */
        if (FontFinder.IsMDL2(variant))
            return SearchMDL2Async(query, variant);

        foreach (SearchTarget target in SearchTarget.KnownTargets)
        {
            if (target.IsTarget(variant))
                return InternalSearchAsync(target.SearchTable, target.TargetType.Name, query, variant);
        }

        /* Generic Unicode Search */
        return SearchUnicodeAsync(query, variant);
    }

    private Task<IReadOnlyList<IGlyphData>> SearchUnicodeAsync(string query, CMFontFace variant)
    {
        return InternalSearchAsync(UNICODE_SEARCH_TABLE, nameof(UnicodeGlyphData), query, variant);
    }

    private Task<IReadOnlyList<IGlyphData>> SearchMDL2Async(string query, CMFontFace variant)
    {
        return InternalSearchAsync(MDL2_SEARCH_TABLE, nameof(MDL2Glyph), query, variant);
    }

    private Task<IReadOnlyList<IGlyphData>> InternalSearchAsync(string ftsTable, string table, string query, CMFontFace variant)
    {
        return Task.Run<IReadOnlyList<IGlyphData>>(() =>
        {
            /* 
             * Step 1: Perform single-result Hex Search if hex
             * Step 2: Perform FTS search if not hex or ambiguous
             * Step 3: Perform LIKE search if still space for results
             */



            // 1. Decide if hex or FTS4 search
            //    If hex, search the main table (UnicodeIndex column is indexed)
            GlyphDescription hexResult = null;

            // 1.1 If single Char, try forcing as hex search
            if (query.Length == 1)
                query = ((uint)query[0]).ToString("x4");

            bool ambiguous = !variant.DirectWriteProperties.IsSymbolFont && IsAmbiguousQuery(query);
            if (hexResult == null && Utils.TryParseHexString(query, out int hex))
            {
                // 1.2. To be more efficient, first check if the font actually contains the UnicodeIndex.
                //      If it does then we ask the database, otherwise we can return without query.
                foreach (var range in variant.UnicodeRanges)
                {
                    if (hex >= range.First && hex <= range.Last)
                    {
                        string hexsql = $"SELECT * FROM {table} WHERE Ix == {hex} LIMIT 1";
                        var hexresults = _connection.GetGlyphData(table, hexsql, query);
                        if (hexresults == null || hexresults.Count == 0)
                        {
                            var label = hex.ToString("x4");
                            hexresults = new()
                            {
                                new GlyphDescription
                                {
                                     UnicodeIndex = hex,
                                     UnicodeHex = label,
                                     Description = label
                                }
                            };
                        }

                        // 1.3. If the search is ambiguous we should still search for description matches,
                        //      otherwise we can return right now
                        //if (!ambiguous)
                        //    return hexresults;
                        //else
                        {
                            hexResult = hexresults.Cast<GlyphDescription>().FirstOrDefault();
                            break;
                        }
                    }
                }

                // 1.4. If the search is ambiguous we should still search for description matches,
                //      otherwise we can return right now with no hex results
                //      If we are a generic symbol font, that's all folks. Time to leave.
                //if (!ambiguous)
                //{
                //    return GlyphService.EMPTY_SEARCH;
                //}
            }

            // 2. If we're performing SQL, create the base query filter
            StringBuilder sb = new();
            bool next = false;

            /// Note: SQLite only supports expression trees up to 1000 items, so we need to limit the range
            /// of Unicode ranges we search through. Very rare for any font to hit this - especially one with
            /// any useful search results. MS Office Symbol is an example of such a font (with no useful search
            /// results anyway). Certain complex Asian script fonts **may** theoretically hit this limit.
            /// We don't want to throw an exception if we ever hit this case, we'll just do our best.
            foreach (var range in variant.UnicodeRanges.Take(995))
            {
                if (next)
                    sb.AppendFormat(range.First != range.Last
                    ? " OR Ix BETWEEN {0} AND {1}"
                    : " OR Ix == {0}", range.First, range.Last);
                else
                {
                    next = true;
                    sb.AppendFormat(range.First != range.Last
                    ? "WHERE (Ix BETWEEN {0} AND {1}"
                    : "WHERE (Ix == {0}", range.First, range.Last);
                }
            }
            sb.Append(")");

            List<IGlyphData> results = new();

            // 3. If the font has a local search map, we should do a text search inside that
            if (variant.SearchMap != null)
            {
                results = variant.SearchMap
                    .Where(c => c.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(SEARCH_LIMIT)
                    .Select(g => new GlyphDescription { UnicodeIndex = (int)g.Key.UnicodeIndex, UnicodeHex = g.Key.UnicodeString, Description = g.Value })
                    .Cast<IGlyphData>()
                    .ToList();
            }

            if (results.Count == SEARCH_LIMIT)
                goto End;

            // 3.1. Otherwise, perform a multi-step text search. First perform an FTS4 search
            int limit = SEARCH_LIMIT - results.Count;

            // 3.2. We need to exclude anything already found above
            string extra = string.Empty;
            if (limit != SEARCH_LIMIT)
            {
                extra = string.Format(" AND Ix NOT IN ({0})", string.Join(", ", results.Select(r => r.UnicodeIndex)));
            }

            /// BENCHMARKING FOR FTFS VS NORMAL TABLES
#if BENCHMARK
            Stopwatch sw = Stopwatch.StartNew();
            object res = null;
            for (int i = 0; i < 100; i++)
            {
                string bsql2 = $"SELECT * FROM {table} {sb.ToString()} AND Description LIKE ? LIMIT {limit}";
                res = _connection.GetGlyphData(table, bsql2, $"%{query}%");
            }
            var time = sw.ElapsedMilliseconds;
            sw.Restart();
            for (int i = 0; i < 100; i++)
            {
                string bsql = $"SELECT * FROM {ftsTable} {sb}{extra} AND Description MATCH ? LIMIT {limit}";
                res = _connection.GetGlyphData(ftsTable, bsql, $"{query}*");
            }
            var time2 = sw.ElapsedMilliseconds;
            Debugger.Break();
#endif


#if USE_FTS
            // 3.3. Execute!
            string sql = $"SELECT * FROM {ftsTable} {sb}{extra} AND Description MATCH ? LIMIT {limit}";
            results.AddRange(_connection.GetGlyphData(ftsTable, sql, $"{query}*"));

            // 4. If we have SEARCH_LIMIT matches, we don't need to perform a partial search and can go home early
            if (results != null && results.Count == SEARCH_LIMIT)
                return InsertHex(results);
#endif

            // 5. Perform a partial search on non-FTS table. Only search for what we need.
            //    This means limit the amount of results, and exclude anything we've already matched.
            limit = SEARCH_LIMIT - results.Count;
            if (limit != SEARCH_LIMIT)
            {
                // 5.1. We need to exclude anything already found above
                sb.AppendFormat("AND Ix NOT IN ({0})",
                    string.Join(", ", results.Select(r => r.UnicodeIndex)));
            }

            // 6. Execute on the non FTS tables
            string sql2 = $"SELECT * FROM {table} {sb.ToString()} AND Description LIKE ? LIMIT {limit}";
            var results2 = _connection.GetGlyphData(table, sql2, $"%{query}%");
            results.AddRange(results2);
            limit = SEARCH_LIMIT - results.Count;

            if (limit <= 0)
                goto End;

            // 7. Check Unihan data
            if (variant.CouldContainUnihan())
            {
                string sql3 = $"SELECT * FROM {nameof(UnihanReading)} {sb.ToString()} AND Type == {(int)UnihanFieldType.Definition} AND Description LIKE ? LIMIT {limit}";
                var results3 = _connection.GetUnihanReadingsByDescription(sql3, $"%{query}%");

                results.AddRange(results3.Select(u =>
                {
                    return new GlyphDescription
                    {
                        Description = u.Description,
                        UnicodeIndex = u.Index,
                        UnicodeHex = u.Index.ToString("X")
                    };
                }));
            }

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
