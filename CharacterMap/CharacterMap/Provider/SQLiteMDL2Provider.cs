using CharacterMap.Services;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Provider
{
    public partial class SQLiteMDL2Provider : IGlyphDataProvider
    {
        const string SEARCH_TABLE = "mdl2search";

        private static SQLiteConnection _searchConnection { get; set; }

        private static TableMapping _searchMapping { get; set; }

        public string GetCharacterDescription(int unicodeIndex)
        {
            var match = _searchConnection.Get<GlyphDescription>(g => g.UnicodeIndex == unicodeIndex);
            return match?.Description;
        }

        public Task InitialiseAsync()
        {
            return InitialiseDebugAsync();
        }
    }

    /* Debug Methods, used for database generation */
    public partial class SQLiteMDL2Provider
    {
        public Task InitialiseDebugAsync()
        {
            return Task.Run(async () =>
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/MDL2.xml"));

                var xml = await XmlDocument.LoadFromFileAsync(file);

                List<(string code, string name)> datas =
                    xml.ChildNodes[0].ChildNodes.Where(n => n.NodeType == NodeType.ElementNode && n.NodeName.Equals("tr")).Select(child =>
                    {
                        var trs = child.ChildNodes.Where(n => n.NodeType == NodeType.ElementNode && n.NodeName.Equals("td")).ToList();
                        return (trs[1].InnerText, trs[2].InnerText);
                    }).ToList();

                // Add any missing types from XAML symbol enum
                var type = typeof(Symbol);
                foreach (var e in Enum.GetValues(type))
                {
                    var name = Enum.GetName(type, e);
                    if (!datas.Any(d => d.name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        datas.Add((((int)e).ToString("X"), name));
                }

                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Mdl2.db");
                if (File.Exists(path))
                    File.Delete(path);

                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
                SQLiteConnectionString connection = new SQLiteConnectionString(path);

                using (SQLiteConnection con = new SQLiteConnection(connection))
                {
                    CreateDefaults(con);
                }

                _searchConnection = new SQLiteConnection(connection);

                {
                    _searchConnection.RunInTransaction(() =>
                    {
                        _searchConnection.InsertAll(datas.Select(d => new GlyphDescription
                        {
                            Description = d.name,
                            UnicodeIndex = int.Parse(d.code, System.Globalization.NumberStyles.HexNumber),
                            UnicodePoint = d.code
                        }));
                    });
                }

                _searchMapping = new TableMapping(typeof(GlyphDescription), CreateFlags.None, SEARCH_TABLE);


            });
        }

        private static void CreateDefaults(SQLiteConnection con)
        {
            

            con.CreateTable<GlyphDescription>();

            con.Execute($"CREATE VIRTUAL TABLE mdl2search USING " +
                $"fts4({nameof(GlyphDescription.UnicodePoint)}, {nameof(GlyphDescription.Description)})");

            con.Execute($"CREATE TRIGGER insert_trigger AFTER INSERT ON {nameof(GlyphDescription)} " +
                $"BEGIN INSERT INTO mdl2search({nameof(GlyphDescription.UnicodePoint)}, {nameof(GlyphDescription.Description)}) " +
                $"VALUES (new.{nameof(GlyphDescription.UnicodePoint)}, new.{nameof(GlyphDescription.Description)}); END;");
        }
    }
}
