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
        private const string SEARCH_TABLE = "mdl2search";

        SQLiteConnection _connection { get; set; }

        public Task InitialiseAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());

            var path = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Data", "GlyphData.db");
            _connection = new SQLiteConnection(new SQLiteConnectionString(path));
            return Task.CompletedTask;

#if DEBUG
            return InitialiseDebugAsync();
#else
            return Task.CompletedTask;
#endif
        }

        public string GetCharacterDescription(int unicodeIndex, FontVariant variant)
        {
            if (FontFinder.IsMDL2(variant))
                return _connection.Get<GlyphDescription>(g => g.UnicodeIndex == unicodeIndex)?.Description;

            if (variant.FontFace.IsSymbolFont)
                return null;

            return _connection.Get<UnicodeGlyphData>(u => u.UnicodeIndex == unicodeIndex)?.Description;
        }
    }


    public partial class SQLiteGlyphProvider 
    {
        public Task InitialiseDebugAsync()
        {
            return Task.Run(async () =>
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "GlyphData.db");
                if (File.Exists(path))
                    File.Delete(path);

                SQLiteConnectionString connection = new SQLiteConnectionString(path);

                using (SQLiteConnection con = new SQLiteConnection(connection))
                {
                    PrepareDatabase(con);
                }

                await PopulateMDL2Async(connection).ConfigureAwait(false);
                await PopulateUnicodeAsync(connection).ConfigureAwait(false);

                using (SQLiteConnection con = new SQLiteConnection(connection))
                {
                    con.Execute("VACUUM \"main\"");
                    con.Execute("VACUUM \"temp\"");
                }

                _connection = new SQLiteConnection(connection);
            });
        }


        private Task PopulateMDL2Async(SQLiteConnectionString connection)
        {
            return Task.Run(async () =>
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/MDL2.xml")).AsTask().ConfigureAwait(false);
                var xml = await XmlDocument.LoadFromFileAsync(file).AsTask().ConfigureAwait(false);

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

                using (var c = new SQLiteConnection(connection))
                {
                    c.RunInTransaction(() =>
                    {
                        c.InsertAll(datas.Select(d => new GlyphDescription
                        {
                            Description = d.name.Humanize(LetterCasing.Title),
                            UnicodeIndex = int.Parse(d.code, System.Globalization.NumberStyles.HexNumber),
                            UnicodePoint = d.code
                        }));
                    });
                }
            });
        }

        private Task PopulateUnicodeAsync(SQLiteConnectionString connection)
        {
                return Task.Run(async () =>
                {
                    using (var c = new SQLiteConnection(connection))
                    {
                        var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/UnicodeData.txt")).AsTask().ConfigureAwait(false);

                        using (var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false))
                        using (var reader = new StreamReader(stream))
                        {
                            string ctrl = "<control>";
                            string[] parts;
                            List<UnicodeGlyphData> data = new List<UnicodeGlyphData>();
                            while (!reader.EndOfStream)
                            {
                                parts = reader.ReadLine().Split(";", StringSplitOptions.None);

                                string hex = parts[0];
                                string desc = parts[1] == ctrl ? parts[10] : parts[1];
                                if (string.IsNullOrWhiteSpace(desc))
                                    desc = parts[1]; // some controls characters are unlabeled
                                int code = Int32.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                                data.Add(new UnicodeGlyphData
                                {
                                    Description = desc.Transform(To.LowerCase, To.TitleCase),
                                    UnicodeGroup = parts[2],
                                    UnicodeHex = hex,
                                    UnicodeIndex = code
                                });
                            }

                            c.RunInTransaction(() => c.InsertAll(data));
                        }
                    }
                });
        }

        private static void PrepareDatabase(SQLiteConnection con)
        {
            con.CreateTable<GlyphDescription>();
            con.CreateTable<UnicodeGlyphData>();

            con.Execute($"CREATE VIRTUAL TABLE mdl2search USING " +
                $"fts4({nameof(GlyphDescription.UnicodePoint)}, {nameof(GlyphDescription.Description)})");

            con.Execute($"CREATE TRIGGER insert_trigger AFTER INSERT ON {nameof(GlyphDescription)} " +
                $"BEGIN INSERT INTO mdl2search({nameof(GlyphDescription.UnicodePoint)}, {nameof(GlyphDescription.Description)}) " +
                $"VALUES (new.{nameof(GlyphDescription.UnicodePoint)}, new.{nameof(GlyphDescription.Description)}); END;");
        }
    }
}
