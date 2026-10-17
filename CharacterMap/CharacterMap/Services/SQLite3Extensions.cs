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
