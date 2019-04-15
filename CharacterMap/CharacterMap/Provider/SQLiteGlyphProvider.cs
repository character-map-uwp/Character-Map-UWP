//#define GENERATE_DATABASE

using CharacterMap.Core;
using CharacterMap.Services;
using Humanizer;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public partial class SQLiteGlyphProvider : IGlyphDataProvider
    {
        internal const string MDL2_SEARCH_TABLE = "mdl2search";
        internal const string UNICODE_SEARCH_TABLE = "unicodesearch";
        const int SEARCH_LIMIT = 10;

        private SQLiteConnection _connection { get; set; }

        public Task InitialiseAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());

#if GENERATE_DATABASE && DEBUG
            /* 
             * If you update the dataset and wish to generate a NEW dataset
             * run the code below
             */

            return InitialiseDebugAsync();
#endif

            var path = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Data", "GlyphData.db");
            _connection = new SQLiteConnection(new SQLiteConnectionString(path, SQLiteOpenFlags.ReadOnly, true));
            return Task.CompletedTask;
        }

        public string GetCharacterDescription(int unicodeIndex, FontVariant variant)
        {
            if (FontFinder.IsMDL2(variant))
                return _connection.Get<GlyphDescription>(g => g.UnicodeIndex == unicodeIndex)?.Description;

            if (variant.FontFace.IsSymbolFont)
                return null;

            return _connection.Get<UnicodeGlyphData>(u => u.UnicodeIndex == unicodeIndex)?.Description;
        }

        public Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult(GlyphService.EMPTY_SEARCH);

            /* MDL2 has special dataset */
            if (FontFinder.IsMDL2(variant))
                return SearchMDL2Async(query, variant);

            /* In the future, FontAwesome can have a special dataset */

            /* We don't label symbol fonts in the main app, so for the most case we don't allow search of them */
            /* If symbol font, go home now */
            if (variant.FontFace.IsSymbolFont)
                return Task.FromResult(GlyphService.EMPTY_SEARCH);

            return SearchUnicodeAsync(query, variant);
        }

        private Task<IReadOnlyList<IGlyphData>> SearchUnicodeAsync(string query, FontVariant variant)
        {
            return InternalSearchAsync(UNICODE_SEARCH_TABLE, nameof(UnicodeGlyphData), query, variant);
        }

        private Task<IReadOnlyList<IGlyphData>> SearchMDL2Async(string query, FontVariant variant)
        {
            return InternalSearchAsync(MDL2_SEARCH_TABLE, nameof(GlyphDescription), query, variant);
        }

        private Task<IReadOnlyList<IGlyphData>> InternalSearchAsync(string ftsTable, string table, string query, FontVariant variant)
        {
            return Task.Run<IReadOnlyList<IGlyphData>>(() =>
            {
                /* Step 1: Use FTS4 (Full-text-search) on SQLite to get full-string matches
                 * Then, if we still have room, do a partial like match 
                 */

                // 1. Create the base query filter
                StringBuilder sb = new StringBuilder();
                bool next = false;
                foreach ((int, int) range in variant.UnicodeRanges)
                {
                    if (next)
                        sb.AppendFormat(" OR UnicodeIndex BETWEEN {0} AND {1}", range.Item1, range.Item2);
                    else
                    {
                        next = true;
                        sb.AppendFormat("WHERE (UnicodeIndex BETWEEN {0} AND {1}", range.Item1, range.Item2);
                    }
                }
                sb.Append(")");

                // 2. Decide if hex or FTS4 search
                // 2.1. If hex, search the main table (UnicodeIndex column is indexed)
                if (Utils.TryParseHexString(query, out int hex))
                {
                    // 2.2. To be more efficient, first check if the font actually contains the UnicodeIndex.
                    //      If it does then we ask the database, otherwise we can return without query.
                    foreach (var range in variant.UnicodeRanges)
                    {
                        if (hex >= range.Item1 && hex <= range.Item2)
                        {
                            string hexsql = $"SELECT * FROM {table} WHERE UnicodeIndex == {hex} LIMIT 1";
                            var hexresults = _connection.Query<GlyphDescription>(hexsql, query)?.Cast<IGlyphData>()?.ToList();
                            if (hexresults == null || hexresults.Count == 0)
                            {
                                var label = hex.ToString("X");
                                hexresults = new List<IGlyphData>()
                                {
                                    new GlyphDescription
                                    {
                                         UnicodeIndex = hex,
                                         UnicodeHex = label,
                                         Description = label
                                    }
                                };
                            }

                            return hexresults;
                        }
                    }

                    return GlyphService.EMPTY_SEARCH;
                }

                // 3. Otherwise, perform a multi-step text search. First perform an FTS4 search
                string sql = $"SELECT * FROM {ftsTable} {sb.ToString()} AND Description MATCH '{query}' LIMIT {SEARCH_LIMIT}";
                var results = _connection.Query<GlyphDescription>(sql, query)?.Cast<IGlyphData>()?.ToList();

                // 4. If we have SEARCH_LIMIT matches, we don't need to perform a partial search and can go home early
                if (results != null && results.Count == SEARCH_LIMIT)
                    return results;

                // 5. Perform a partial search on non-FTS table. Only search for what we need.
                //    This means limit the amount of results, and exclude anything we've already matched.
                int limit = results == null ? SEARCH_LIMIT : SEARCH_LIMIT - results.Count;
                if (limit != SEARCH_LIMIT)
                {
                    // 5.1. We need to exclude anything already found above
                    sb.AppendFormat("AND UnicodeIndex NOT IN ({0})",
                        string.Join(", ", results.Select(r => r.UnicodeIndex)));
                }

                // 6. Execute on the non FTS tables
                string sql2 = $"SELECT * FROM {table} {sb.ToString()} AND Description LIKE '%{query}%' LIMIT {limit}";
                var results2 = _connection.Query<GlyphDescription>(sql2, query)?.Cast<IGlyphData>()?.ToList();

                if (results != null)
                {
                    results.AddRange(results2);
                    return results.AsReadOnly();
                }
                else
                    return results2;
            });
        }
    }
}
