using SQLite;

namespace CharacterMap.Provider;

public class SQLiteCollectionProvider : ICollectionProvider
{
    private SQLiteConnection _conn { get; set; }

    private PreparedSqlLiteInsertCommand _ins { get; set; }
    private PreparedSqlLiteInsertCommand _smins { get; set; }

    private PreparedSqlLiteInsertCommand GetInsert(IFontCollection c) => c is UserFontCollection ? _ins : _smins;
    private string GetTable(IFontCollection c) => c is UserFontCollection ? "Collections" : "SmartCollections";
    private string GetArgs(IFontCollection c) => c is UserFontCollection ? "Fonts" : "Filters";


    public Task<IReadOnlyList<IFontCollection>> LoadCollectionsAsync()
    {
        PrepareConnection();
        List<IFontCollection> collections = [];

        try
        {
            var cols = _conn.CreateCommand("SELECT * FROM \"Collections\" ORDER BY Name").ReadAsSQLFontCollections();
            foreach (var c in cols)
                collections.Add(c.AsUserFontCollection());

            var smarts = _conn.CreateCommand("SELECT * FROM \"SmartCollections\" ORDER BY Name").ReadAsSQLSmartCollections();
            foreach (var c in smarts)
                collections.Add(c.AsSmartFontCollection());
        }
        catch (Exception ex) when (ex.Message.StartsWith("no such table"))
        {
            // Workaround for #275, though there's no reasonable explanation
            // for ever being able to get in this state.
            CreateCollectionsTable();
            return LoadCollectionsAsync();
        }

        return Task.FromResult((IReadOnlyList<IFontCollection>)collections);
    }

    public async Task<bool> DeleteCollectionAsync(IFontCollection collection)
    {
        PrepareConnection();

        if (1 == _conn.Execute($"DELETE FROM \"{GetTable(collection)}\" WHERE Id = ?", collection.Id))
        {
            await VacuumAsync();
            return true;
        }

        return false;
    }

    

    public Task SaveCollectionAsync(IFontCollection c)
    {
        return Task.Run(() =>
        {
            PrepareConnection();

            // If Id == 0 we create a new collection
            if (c.Id == 0)
            {
                var obs = new object[2];
                obs[0] = c.Name;
                obs[1] = c.GetFlatArgs();

                GetInsert(c).ExecuteNonQuery(obs);
                c.Id = SQLite3.LastInsertRowid(_conn.Handle);
            }
            else
            {
                // Otherwise update existing collection
                _conn.CreateCommand(
                         $"UPDATE \"{GetTable(c)}\" Set Name = ?, {GetArgs(c)} = ? WHERE Id = ?", c.Name, c.GetFlatArgs(), c.Id)
                     .ExecuteNonQuery();
            }
        });
    }

    private void CreateCollectionsTable()
    {
        string create = "CREATE TABLE IF NOT EXISTS \"Collections\" (\r\n\"Id\" integer primary key autoincrement not null ,\r\n\"Name\" varchar ,\r\n\"Fonts\" varchar )";
        _conn.Execute(create);

        string create2 = "CREATE TABLE IF NOT EXISTS \"SmartCollections\" (\r\n\"Id\" integer primary key autoincrement not null ,\r\n\"Name\" varchar ,\r\n\"Filters\" varchar )";
        _conn.Execute(create2);
    }

    public Task StoreMigrationAsync(List<UserFontCollection> collections)
    {
        PrepareConnection();

        // 1. Create Collections Table
        //CreateCollectionsTable();

        // 2. Insert old collections into Database
        var obs = new object[2];
        _conn.RunInTransaction(() =>
        {
            foreach (var c in collections)
            {
                obs[0] = c.Name;
                obs[1] = string.Join('', c.Fonts);
                _ins.ExecuteNonQuery(obs);
            }
        });

        Checkpoint();

        ResourceHelper.AppSettings.HasSQLiteCollections = true;
        return Task.CompletedTask;
    }

    private void PrepareConnection()
    {
        if (_conn is null)
        {
            _conn = new SQLiteConnection(Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Collections.db"))
                .EnableWriteAheadLogging()
                .SetSynchronousNormal();

            _ins = new(_conn, "INSERT INTO \"Collections\" (Name, Fonts) VALUES (?, ?)");
            _smins = new(_conn, "INSERT INTO \"SmartCollections\" (Name, Filters) VALUES (?, ?)");

            CreateCollectionsTable();
        }
    }

    private Task VacuumAsync() => Task.Run(() => _conn?.Vacuum());

    /// <summary>
    /// Commits the WAL-journal
    /// </summary>
    private void Checkpoint() => _conn?.ExecuteScalarStr("PRAGMA wal_checkpoint(TRUNCATE)");

    public Task FlushAsync()
    {
        Checkpoint();
        return Task.CompletedTask;
    }
}
