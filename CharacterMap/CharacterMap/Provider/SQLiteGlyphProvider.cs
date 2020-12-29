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
using CharacterMap.Models;

namespace CharacterMap.Provider
{
    public partial class SQLiteGlyphProvider : IGlyphDataProvider
    {
        internal const string FONTAWESOME_SEARCH_TABLE = "fontawesomesearch";
        internal const string ICOFONT_SEARCH_TABLE = "icfsearch";
        internal const string MATERIAL_DESIGN_ICONS_LEGACY_SEARCH_TABLE = "mdilegacysearch";
        internal const string MATERIAL_DESIGN_ICONS_SEARCH_TABLE = "mdisearch";
        internal const string MDL2_SEARCH_TABLE = "mdl2search";
        internal const string UNICODE_SEARCH_TABLE = "unicodesearch";
        internal const string WEBDINGS_SEARCH_TABLE = "wbdsearch";
        internal const string WINGDINGS_SEARCH_TABLE = "wngsearch";
        internal const string WINGDINGS2_SEARCH_TABLE = "wng2search";
        internal const string WINGDINGS3_SEARCH_TABLE = "wng3search";

        private int SEARCH_LIMIT => new AppSettings().MaxSearchResult;

        private SQLiteConnection _connection { get; set; }

        public void Initialise()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var path = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Data", "GlyphData.db");
            _connection = new SQLiteConnection(new SQLiteConnectionString(path, SQLiteOpenFlags.ReadOnly, true));
        }

#if DEBUG
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

        public string GetCharacterDescription(int unicodeIndex, FontVariant variant)
        {
            string desc = null;

            // MDL2 has it's own special logic
            if (FontFinder.IsMDL2(variant))
            {
                desc = _connection.Get<MDL2Glyph>(g => g.UnicodeIndex == unicodeIndex)?.Description;
                if (string.IsNullOrWhiteSpace(desc) && Enum.IsDefined(typeof(Symbol), unicodeIndex))
                    return ((Symbol)unicodeIndex).ToString().Humanize(LetterCasing.Title);
                return desc;
            }

            // Otherwise check if we have a search table for this font
            foreach (var target in SearchTarget.KnownTargets)
            {
                if (target.IsTarget(variant))
                {
                    var map = _connection.GetMapping(target.TargetType);
                    var items = _connection.Query(map, $"SELECT * FROM {target.SearchTable} WHERE Ix = ? LIMIT 1", unicodeIndex);
                    desc = (items.FirstOrDefault() as GlyphDescription)?.Description;

                    break;
                }
            }

            // Otherwise get a fallback value
            if (string.IsNullOrEmpty(desc))
                desc = _connection.Get<UnicodeGlyphData>(u => u.UnicodeIndex == unicodeIndex)?.Description;

            return desc;
        }

        #region SEARCH

        public Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant)
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

        private void LocalSearch(string query, FontVariant variant)
        {
            throw new NotImplementedException();
        }

        private Task<IReadOnlyList<IGlyphData>> SearchUnicodeAsync(string query, FontVariant variant)
        {
            return InternalSearchAsync(UNICODE_SEARCH_TABLE, nameof(UnicodeGlyphData), query, variant);
        }

        private Task<IReadOnlyList<IGlyphData>> SearchMDL2Async(string query, FontVariant variant)
        {
            return InternalSearchAsync(MDL2_SEARCH_TABLE, nameof(MDL2Glyph), query, variant);
        }

        private Task<IReadOnlyList<IGlyphData>> InternalSearchAsync(string ftsTable, string table, string query, FontVariant variant)
        {
            return Task.Run<IReadOnlyList<IGlyphData>>(() =>
            {
                /* 
                 * Step 1: Perform single-result Hex Search if hex
                 * Step 2: Perform FTS search if not hex or ambiguous
                 * Step 3: Perform LIKE search if still space for results
                 */

                // 1. Decide if hex or FTS4 search
                // 1.1. If hex, search the main table (UnicodeIndex column is indexed)
                GlyphDescription hexResult = null;
                bool ambiguous = !variant.FontFace.IsSymbolFont && IsAmbiguousQuery(query);
                if (Utils.TryParseHexString(query, out int hex))
                {
                    // 1.2. To be more efficient, first check if the font actually contains the UnicodeIndex.
                    //      If it does then we ask the database, otherwise we can return without query.
                    foreach (var range in variant.UnicodeRanges)
                    {
                        if (hex >= range.First && hex <= range.Last)
                        {
                            string hexsql = $"SELECT * FROM {table} WHERE Ix == {hex} LIMIT 1";
                            var hexresults = _connection.Query<GlyphDescription>(hexsql, query)?.Cast<IGlyphData>()?.ToList();
                            if (hexresults == null || hexresults.Count == 0)
                            {
                                var label = hex.ToString("x4");
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
                            
                            // 1.3. If the search is ambiguous we should still search for description matches,
                            //      otherwise we can return right now
                            if (!ambiguous)
                                return hexresults;
                            else
                            {
                                hexResult = hexresults.Cast<GlyphDescription>().FirstOrDefault();
                                break;
                            }
                                
                        }
                    }

                    // 1.4. If the search is ambiguous we should still search for description matches,
                    //      otherwise we can return right now with no hex results
                    //      If we are a generic symbol font, that's all folks. Time to leave.
                    if (!ambiguous)
                    {
                        return GlyphService.EMPTY_SEARCH;
                    }
                }

                // 2. If we're performing SQL, create the base query filter
                StringBuilder sb = new StringBuilder();
                bool next = false;

                /// Note: SQLite only supports expression trees up to 1000 items, so we need to limit the range
                /// of Unicode ranges we search through. Very rare for any font to hit this - especially one with
                /// any useful search results. MS Office Symbol is an example of such a font (with no useful search
                /// results anyway). Certain complex Asian script fonts **may** theoretically hit this limit.
                /// We don't want to throw an exception if we ever hit this case, we'll just do our best.
                foreach (var range in variant.UnicodeRanges.Take(995)) 
                {
                    if (next)
                        sb.AppendFormat(" OR Ix BETWEEN {0} AND {1}", range.First, range.Last);
                    else
                    {
                        next = true;
                        sb.AppendFormat("WHERE (Ix BETWEEN {0} AND {1}", range.First, range.Last);
                    }
                }
                sb.Append(")");

                // 2.1. A helper method to inject the hex result for ambiguous searches
                List<IGlyphData> InsertHex(List<IGlyphData> list)
                {
                    if (hexResult != null)
                        list.Insert(0, hexResult);
                    return list;
                }

                List<IGlyphData> results = new List<IGlyphData>();

                // 3. If the font has a local search map, we should do a text search inside that
                if (variant.SearchMap != null)
                {
                    results = variant.SearchMap
                        .Where(c => c.Value.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .Take(SEARCH_LIMIT)
                        .Select(g => new GlyphDescription { UnicodeIndex = (int)g.Key.UnicodeIndex, UnicodeHex = g.Key.UnicodeString, Description = g.Value.Name })
                        .Cast<IGlyphData>()
                        .ToList();
                }

                if (results.Count == SEARCH_LIMIT)
                    return results;

                // 3.1. Otherwise, perform a multi-step text search. First perform an FTS4 search
                int limit = results == null ? SEARCH_LIMIT : SEARCH_LIMIT - results.Count;

                // 3.2. We need to exclude anything already found above
                string extra = string.Empty;
                if (limit != SEARCH_LIMIT)
                {
                    
                    extra = string.Format(" AND Ix NOT IN ({0})", string.Join(", ", results.Select(r => r.UnicodeIndex)));
                }

                // 3.3. Execute!
                string sql = $"SELECT * FROM {ftsTable} {sb}{extra} AND Description MATCH ? LIMIT {limit}";
                results.AddRange(_connection.Query<GlyphDescription>(sql, $"{query}*")?.Cast<IGlyphData>());

                // 4. If we have SEARCH_LIMIT matches, we don't need to perform a partial search and can go home early
                if (results != null && results.Count == SEARCH_LIMIT)
                {
                    return InsertHex(results);
                }

                // 5. Perform a partial search on non-FTS table. Only search for what we need.
                //    This means limit the amount of results, and exclude anything we've already matched.
                limit = results == null ? SEARCH_LIMIT : SEARCH_LIMIT - results.Count;
                if (limit != SEARCH_LIMIT)
                {
                    // 5.1. We need to exclude anything already found above
                    sb.AppendFormat("AND Ix NOT IN ({0})",
                        string.Join(", ", results.Select(r => r.UnicodeIndex)));
                }

                // 6. Execute on the non FTS tables
                string sql2 = $"SELECT * FROM {table} {sb.ToString()} AND Description LIKE ? LIMIT {limit}";
                var results2 = _connection.Query<GlyphDescription>(sql2, $"%{query}%")?.Cast<IGlyphData>()?.ToList();

                if (results != null)
                {
                    results.AddRange(results2);
                    return InsertHex(results);
                }
                else
                    return InsertHex(results2);
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
}
