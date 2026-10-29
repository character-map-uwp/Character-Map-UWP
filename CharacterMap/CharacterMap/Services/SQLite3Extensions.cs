using CharacterMapCX;
using SQLite;

namespace CharacterMap.Services;

#pragma warning disable CS9113 // Parameter is unread locally, but used by SourceGen in a seperate project

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SQLReaderAttribute<T>(string Name, bool IsSingle = false) : Attribute where T : new() { }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SQLReaderMappingAttribute<T>(string Property, Type readType = null, int columnIndex = -1) : Attribute { }

#pragma warning restore CS9113 // Parameter is unread locally, but used by SourceGen in a seperate project

public static class SQLite3Extensions
{
    /// <summary>
    /// Ranges separated by at most this many codepoints are coalesced into a single
    /// PRIMARY KEY B-tree scan (likely on the same leaf page) and filtered in memory,
    /// while larger gaps trigger a new B-tree seek to skip unmapped table regions.
    /// </summary>
    private const uint COALESCE_GAP_THRESHOLD = 128;

    public static bool ContainsCodepoint(DWriteUnicodeRange[] ranges, uint cp)
    {
        if (ranges is null || ranges.Length == 0)
            return false;

        int low = 0;
        int high = ranges.Length - 1;

        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            DWriteUnicodeRange r = ranges[mid];

            if (cp < r.First)
                high = mid - 1;
            else if (cp > r.Last)
                low = mid + 1;
            else
                return true;
        }

        return false;
    }

    private static bool ContainsUnicodeIndex(List<IGlyphData> list, int countToCheck, int unicodeIndex)
    {
        for (int i = 0; i < countToCheck; i++)
        {
            if (list[i].UnicodeIndex == unicodeIndex)
                return true;
        }

        return false;
    }

    public static void SearchGlyphDescriptions(
        this SQLiteConnection c,
        string table,
        DWriteUnicodeRange[] ranges,
        string likeQuery,
        int targetTotalCount,
        List<IGlyphData> results)
    {
        if (ranges is null || ranges.Length == 0 || results.Count >= targetTotalCount)
            return;

        int existingCount = results.Count;
        bool isUnicodeTable = table == nameof(UnicodeGlyphData);

        string sql = $"SELECT Ix, Hx, Description FROM \"{table}\" WHERE Ix BETWEEN ? AND ? AND Description LIKE ?";
        SQLiteCommand cmd = c.CreateCommand(sql, 0, 0, likeQuery);
        SQLitePCL.sqlite3_stmt stmt = cmd.Prepare();
        try
        {
            int rIdx = 0;
            while (rIdx < ranges.Length && results.Count < targetTotalCount)
            {
                uint spanFirst = ranges[rIdx].First;
                uint spanLast = ranges[rIdx].Last;
                bool hasInternalGaps = false;

                while (rIdx + 1 < ranges.Length && ranges[rIdx + 1].First <= spanLast + COALESCE_GAP_THRESHOLD)
                {
                    rIdx++;
                    spanLast = ranges[rIdx].Last;
                    hasInternalGaps = true;
                }
                rIdx++;

                SQLite3.Reset(stmt);
                SQLite3.BindInt(stmt, 1, (int)spanFirst);
                SQLite3.BindInt(stmt, 2, (int)spanLast);

                while (SQLite3.Step(stmt) == SQLite3.Result.Row)
                {
                    int ix = SQLite3.ColumnInt(stmt, 0);
                    if (hasInternalGaps && !ContainsCodepoint(ranges, (uint)ix))
                        continue;

                    if (existingCount > 0 && ContainsUnicodeIndex(results, existingCount, ix))
                        continue;

                    string hx = SQLite3.ColumnString(stmt, 1);
                    string desc = SQLite3.ColumnString(stmt, 2);

                    if (isUnicodeTable)
                    {
                        results.Add(new UnicodeGlyphData
                        {
                            UnicodeIndex = ix,
                            UnicodeHex = hx,
                            Description = desc
                        });
                    }
                    else
                    {
                        results.Add(new GlyphDescription
                        {
                            UnicodeIndex = ix,
                            UnicodeHex = hx,
                            Description = desc
                        });
                    }

                    if (results.Count >= targetTotalCount)
                        break;
                }
            }
        }
        finally
        {
            stmt.Dispose();
        }
    }

    public static void SearchUnihanDefinitions(
        this SQLiteConnection c,
        DWriteUnicodeRange[] ranges,
        string likeQuery,
        int targetTotalCount,
        List<IGlyphData> results)
    {
        if (ranges is null || ranges.Length == 0 || results.Count >= targetTotalCount)
            return;

        if (ranges[^1].Last < Unicode.UNIHAN_IDX)
            return;

        int existingCount = results.Count;

        // Binary search to find the first range that reaches Unicode.UNIHAN_IDX
        int low = 0;
        int high = ranges.Length - 1;
        while (low < high)
        {
            int mid = (low + high) >>> 1;
            if (ranges[mid].Last < Unicode.UNIHAN_IDX)
                low = mid + 1;
            else
                high = mid;
        }

        const string sql = "SELECT Ix, Definition FROM UnihanReading WHERE Ix BETWEEN ? AND ? AND Definition LIKE ?";
        SQLiteCommand cmd = c.CreateCommand(sql, 0, 0, likeQuery);
        SQLitePCL.sqlite3_stmt stmt = cmd.Prepare();
        try
        {
            int rIdx = low;
            while (rIdx < ranges.Length && results.Count < targetTotalCount)
            {
                uint spanFirst = Math.Max(ranges[rIdx].First, Unicode.UNIHAN_IDX);
                uint spanLast = ranges[rIdx].Last;
                bool hasInternalGaps = false;

                while (rIdx + 1 < ranges.Length && ranges[rIdx + 1].First <= spanLast + COALESCE_GAP_THRESHOLD)
                {
                    rIdx++;
                    spanLast = ranges[rIdx].Last;
                    hasInternalGaps = true;
                }
                rIdx++;

                SQLite3.Reset(stmt);
                SQLite3.BindInt(stmt, 1, (int)spanFirst);
                SQLite3.BindInt(stmt, 2, (int)spanLast);

                while (SQLite3.Step(stmt) == SQLite3.Result.Row)
                {
                    int ix = SQLite3.ColumnInt(stmt, 0);
                    if (hasInternalGaps && !ContainsCodepoint(ranges, (uint)ix))
                        continue;

                    if (existingCount > 0 && ContainsUnicodeIndex(results, existingCount, ix))
                        continue;

                    string desc = SQLite3.ColumnString(stmt, 1);
                    results.Add(new GlyphDescription
                    {
                        UnicodeIndex = ix,
                        UnicodeHex = ix.ToString("X"),
                        Description = desc
                    });

                    if (results.Count >= targetTotalCount)
                        break;
                }
            }
        }
        finally
        {
            stmt.Dispose();
        }
    }

    public static List<GlyphDescription> GetGlyphData(this SQLiteConnection c, string table, string sql, string query)
    {
        var cmd = c.CreateCommand(sql, query);
        return table == "UnicodeGlyphData"
            ? cmd.ReadAsUnicodeGlyphDatas()
            : cmd.ReadAsGlyphDescriptions();
    }

    public static List<UnihanReading> GetUnihanReadings(this SQLiteConnection c, int idx)
    {
        SQLiteCommand cmd = c.CreateCommand("SELECT Definition, Readings FROM UnihanReading WHERE Ix = ? LIMIT 1", idx);
        var stmt = cmd.Prepare();
        try
        {
            if (SQLite3.Step(stmt) == SQLite3.Result.Row)
            {
                List<UnihanReading> list = [];
                string def = SQLite3.ColumnString(stmt, 0);
                if (!string.IsNullOrEmpty(def))
                    list.Add(new(idx, UnihanFieldType.Definition, def));

                string readings = SQLite3.ColumnString(stmt, 1);
                if (!string.IsNullOrEmpty(readings))
                {
                    string[] lines = readings.Split('\n');
                    foreach (string line in lines)
                    {
                        int tabIdx = line.IndexOf('\t');
                        if (tabIdx > 0 && int.TryParse(line.Substring(0, tabIdx), out int typeVal))
                            list.Add(new(idx, (UnihanFieldType)typeVal, line[(tabIdx + 1)..]));
                    }
                }
                return list;
            }
        }
        finally
        {
            stmt.Dispose();
        }

        return [];
    }

    public static List<GlyphDescription> GetUnihanDefinitionsByDescription(this SQLiteConnection c, string sql, string query)
    {
        SQLiteCommand cmd = c.CreateCommand(sql, query);
        var stmt = cmd.Prepare();
        try
        {
            List<GlyphDescription> list = [];
            while (SQLite3.Step(stmt) == SQLite3.Result.Row)
            {
                int ix = SQLite3.ColumnInt(stmt, 0);
                string desc = SQLite3.ColumnString(stmt, 1);
                list.Add(new()
                {
                    UnicodeIndex = ix,
                    UnicodeHex = ix.ToString("X"),
                    Description = desc
                });
            }
            return list;
        }
        finally
        {
            stmt.Dispose();
        }
    }

    public static AdobeGlyphListMapping GetGlyphListMapping(this SQLiteConnection c, string name)
    {
        return c.CreateCommand("SELECT * FROM AdobeGlyphListMapping WHERE S = ? LIMIT 1", name)
                .ReadAsAdobeGlyphListMapping();
    }

    private static readonly object _descLock = new();
    private static readonly Dictionary<string, SQLitePCL.sqlite3_stmt> _descStatements = [];

    public static string GetUnicodeDescription(this SQLiteConnection c, int index, string table = "UnicodeGlyphData")
    {
        lock (_descLock)
        {
            if (!_descStatements.TryGetValue(table, out SQLitePCL.sqlite3_stmt stmt))
            {
                SQLiteCommand cmd = c.CreateCommand($"SELECT Description FROM \"{table}\" WHERE Ix = ?", 0);
                stmt = cmd.Prepare();
                _descStatements[table] = stmt;
            }

            SQLite3.Reset(stmt);
            SQLite3.BindInt(stmt, 1, index);

            if (SQLite3.Step(stmt) == SQLite3.Result.Row)
                return SQLite3.ColumnString(stmt, 0);

            return null;
        }
    }




    //------------------------------------------------------
    //
    // Source Generator Shims
    // - Used to source gen the "ReadAs{XXXX}" methods
    // - Shims will be removed by compiler
    //
    //------------------------------------------------------

    [SQLReader<SQLiteFontCollection>("SQLFontCollection")]
    [SQLReaderMapping<int>(nameof(SQLiteFontCollection.Id))]
    [SQLReaderMapping<string>(nameof(SQLiteFontCollection.Name))]
    [SQLReaderMapping<string>(nameof(SQLiteFontCollection.Fonts))]
    private class Shim0 : Object { }

    [SQLReader<SQLiteSmartFontCollection>("SQLSmartCollection")]
    [SQLReaderMapping<int>(nameof(SQLiteSmartFontCollection.Id))]
    [SQLReaderMapping<string>(nameof(SQLiteSmartFontCollection.Name))]
    [SQLReaderMapping<string>(nameof(SQLiteSmartFontCollection.Filters))]
    private class Shim00 : Object { }

    [SQLReader<GlyphDescription>("UnicodeGlyphData")]
    [SQLReaderMapping<int>(nameof(GlyphDescription.UnicodeIndex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.UnicodeHex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.Description))]
    private class Shim1 : Object { }

    [SQLReader<GlyphDescription>(nameof(GlyphDescription))]
    [SQLReaderMapping<int>(nameof(GlyphDescription.UnicodeIndex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.UnicodeHex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.Description))]
    private class Shim2 : Object { }

    [SQLReader<AdobeGlyphListMapping>(nameof(AdobeGlyphListMapping), true)]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex))]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex2))]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex3))]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex4))]
    private class Shim4 : Object { }
}
