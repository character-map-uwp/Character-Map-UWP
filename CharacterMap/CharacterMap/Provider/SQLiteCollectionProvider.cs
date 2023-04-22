using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace CharacterMap.Provider
{
    public class SQLiteCollectionProvider : ICollectionProvider
    {
        private SQLiteConnection _conn { get; set; }

        private PreparedSqlLiteInsertCommand _ins { get; set; }

        public Task<List<UserFontCollection>> LoadCollectionsAsync()
        {
            PrepareConnection();

            List<UserFontCollection> collections = new();

            var cols = _conn.CreateCommand("SELECT * FROM \"Collections\" ORDER BY Name").AsCollections();
            foreach (var c in cols)
                collections.Add(c.AsUserFontCollection());

            return Task.FromResult(collections);
        }

        public async Task<bool> DeleteCollectionAsync(UserFontCollection collection)
        {
            PrepareConnection();
            if (1 == _conn.Execute($"DELETE FROM \"Collections\" WHERE Id = ?", collection.Id))
            {
                await VacuumAsync();
                return true;
            }

            return false;
        }
        
        public Task SaveCollectionAsync(UserFontCollection collection)
        {
            return Task.Run(() =>
            {
                PrepareConnection();

                if (collection.Id == 0)
                {
                    var obs = new object[2];
                    obs[0] = collection.Name;
                    obs[1] = collection.GetFlatFonts();

                    _ins.ExecuteNonQuery(obs);
                    collection.Id = SQLite3.LastInsertRowid(_conn.Handle);
                }
                else
                {
                    var cmd = _conn.CreateCommand("UPDATE \"Collections\" Set Name = ?, Fonts = ? WHERE Id = ?",
                        collection.Name, collection.GetFlatFonts(), collection.Id);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public Task StoreMigrationAsync(List<UserFontCollection> collections)
        {
            PrepareConnection();

            // 1. Create Collections Table
            string create = "CREATE TABLE IF NOT EXISTS \"Collections\" (\r\n\"Id\" integer primary key autoincrement not null ,\r\n\"Name\" varchar ,\r\n\"Fonts\" varchar )";
            _conn.Execute(create);

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
            }
        }

        private Task VacuumAsync() => Task.Run(() => _conn.Vacuum());

        /// <summary>
        /// Commits the WAL-journal
        /// </summary>
        private void Checkpoint() => _conn.ExecuteScalarStr("PRAGMA wal_checkpoint(TRUNCATE)");

        public Task FlushAsync()
        {
            Checkpoint();
            return Task.CompletedTask;
        }
    }
}
