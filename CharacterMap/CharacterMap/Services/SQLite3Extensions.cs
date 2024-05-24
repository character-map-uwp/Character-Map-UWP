using SQLite;

namespace CharacterMap.Services;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SQLReaderAttribute<T>(string Name, bool IsSingle = false) : Attribute where T : new() { }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SQLReaderMappingAttribute<T>(string Property, Type readType = null, int columnIndex = -1) : Attribute { }

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
        return c.CreateCommand("SELECT * FROM UnihanReading WHERE Ix = ?", idx)
                .ReadAsUnihanReadings();
    }

    public static List<UnihanReading> GetUnihanReadingsByDescription(this SQLiteConnection c, string sql, string query)
    {
        return c.CreateCommand(sql, query)
                .ReadAsUnihanReadings();
    }

    public static AdobeGlyphListMapping GetGlyphListMapping(this SQLiteConnection c, string name)
    {
        return c.CreateCommand("SELECT * FROM AdobeGlyphListMapping WHERE S = ? LIMIT 1", name)
                .ReadAsAdobeGlyphListMapping();
    }

    public static string GetUnicodeDescription(this SQLiteConnection c, int index, string table = "UnicodeGlyphData")
    {
        var cmd = c.CreateCommand($"SELECT Description FROM \"{table}\" WHERE Ix = ?", index);
        var stmt = cmd.Prepare();

        try
        {
            if (SQLite3.Step(stmt) == SQLite3.Result.Row)
                return SQLite3.ColumnString(stmt, 0);
        }
        finally
        {
            stmt.Dispose();
        }

        return null;
    }


    
    
    //------------------------------------------------------
    //
    // Source Generator Shims
    // - Used to source gen the "ReadAs{XXXX}" methods
    // - Shims will be removed by compiler
    //
    //------------------------------------------------------

    [SQLReader<GlyphDescription>("UnicodeGlyphData")]
    [SQLReaderMapping<int>(nameof(GlyphDescription.UnicodeIndex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.UnicodeHex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.Description), typeof(string), 3)]
    private class Shim1 : Object { }

    [SQLReader<GlyphDescription>(nameof(GlyphDescription))]
    [SQLReaderMapping<int>(nameof(GlyphDescription.UnicodeIndex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.UnicodeHex))]
    [SQLReaderMapping<string>(nameof(GlyphDescription.Description))]
    private class Shim2 : Object { }

    [SQLReader<UnihanReading>(nameof(UnihanReading))]
    [SQLReaderMapping<int>(nameof(UnihanReading.Index))]
    [SQLReaderMapping<UnihanFieldType>(nameof(UnihanReading.Type), typeof(int))]
    [SQLReaderMapping<string>(nameof(UnihanReading.Description))]
    private class Shim3 : Object { }

    [SQLReader<AdobeGlyphListMapping>(nameof(AdobeGlyphListMapping), true)]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex))]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex2))]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex3))]
    [SQLReaderMapping<int>(nameof(AdobeGlyphListMapping.UnicodeIndex4))]
    private class Shim4 : Object { }
}
