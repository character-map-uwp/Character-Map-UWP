using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using Humanizer;
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

    /* 
     * These are DEV time only methods to create a new database when data changes.
     * After creating a new database copy it to the Assets/Data/ folder to ship it with
     * the app
     */
#if DEBUG

    internal class FabricGlyph
    {
        public string Name { get; set; }
        public string Unicode { get; set; }
    }

    public partial class SQLiteGlyphProvider
    {
        public Task InitialiseDebugAsync()
        {
            return Task.Run(async () =>
            {
                var path = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "GlyphData.db");
                if (File.Exists(path))
                    File.Delete(path);

                SQLiteConnectionString connection = new SQLiteConnectionString(path);

                using (SQLiteConnection con = new SQLiteConnection(connection))
                {
                    PrepareDatabase(con);
                }

                await PopulateMDL2Async(connection).ConfigureAwait(false);
                await PopulateUnicodeAsync(connection).ConfigureAwait(false);
                await PopulateFontAwesomeAsync(connection).ConfigureAwait(false);

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
                /* Based on the MDL2 listings taken from the Github source of the 
                 * Windows Developer documentation
                 */
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


                /* read fabric mdl2 listing */
                var fabric = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/FabricMDL2.json")).AsTask().ConfigureAwait(false);
                List<FabricGlyph> glyphs = await Json.ReadAsync<List<FabricGlyph>>(fabric);
                foreach (var glyph in glyphs)
                {
                    if (!datas.Any(d => d.code.Equals(glyph.Unicode)))
                        datas.Add((glyph.Unicode, glyph.Name));
                }

                /* read manually created full mdl2 listings */
                var manual = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/FullMDL2ManualList.txt")).AsTask().ConfigureAwait(false);
                using (var stream = await manual.OpenStreamForReadAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                {
                    string[] parts;
                    while (!reader.EndOfStream)
                    {
                        parts = reader.ReadLine().Split(":", StringSplitOptions.None);

                        if (!datas.Any(d => d.code.Equals(parts[1], StringComparison.OrdinalIgnoreCase)))
                            datas.Add((parts[1], parts[0]));
                    }
                }

                var data = datas.Select(d => new MDL2Glyph
                {
                    Description = d.name.Humanize(LetterCasing.Title),
                    UnicodeIndex = int.Parse(d.code, System.Globalization.NumberStyles.HexNumber),
                    UnicodeHex = d.code
                }).OrderBy(g => g.UnicodeIndex).ToList();

                using (var c = new SQLiteConnection(connection))
                {
                    c.RunInTransaction(() =>
                    {
                        c.InsertAll(data);
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

        private Task PopulateFontAwesomeAsync(SQLiteConnectionString connection)
        {
            return Task.Run(async () =>
            {
                using (var c = new SQLiteConnection(connection))
                {
                    var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/FontAwesome.txt")).AsTask().ConfigureAwait(false);

                    using (var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(stream))
                    {
                        string[] parts;
                        List<FontAwesomeGlyph> data = new List<FontAwesomeGlyph>();
                        while (!reader.EndOfStream)
                        {
                            parts = reader.ReadLine().Split("	", StringSplitOptions.None);

                            string desc = parts[1];
                            string hex = parts[2];
                            int code = Int32.Parse(hex, System.Globalization.NumberStyles.HexNumber);

                            data.Add(new FontAwesomeGlyph
                            {
                                Description = desc.Humanize(LetterCasing.Title),
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
            con.CreateTable<MDL2Glyph>();
            con.CreateTable<UnicodeGlyphData>();
            con.CreateTable<FontAwesomeGlyph>();

            /* MDL2 SEARCH */
            con.Execute($"CREATE VIRTUAL TABLE {MDL2_SEARCH_TABLE} USING " +
                $"fts4(Ix, Hx, {nameof(IGlyphData.Description)})");

            con.Execute($"CREATE TRIGGER insert_trigger AFTER INSERT ON {nameof(MDL2Glyph)} " +
                $"BEGIN INSERT INTO {MDL2_SEARCH_TABLE}(Ix, Hx, {nameof(IGlyphData.Description)}) " +
                $"VALUES (new.Ix, new.Hx, new.{nameof(IGlyphData.Description)}); END;");



            /* FONT AWESOME SEARCH */
            con.Execute($"CREATE VIRTUAL TABLE {FONTAWESOME_SEARCH_TABLE} USING " +
                $"fts4(Ix, Hx, {nameof(IGlyphData.Description)})");

            con.Execute($"CREATE TRIGGER insert_trigger_fa AFTER INSERT ON {nameof(FontAwesomeGlyph)} " +
                $"BEGIN INSERT INTO {FONTAWESOME_SEARCH_TABLE}(Ix, Hx, {nameof(IGlyphData.Description)}) " +
                $"VALUES (new.Ix, new.Hx, new.{nameof(IGlyphData.Description)}); END;");



            /* UNICODE SEARCH */
            con.Execute($"CREATE VIRTUAL TABLE {UNICODE_SEARCH_TABLE} USING " +
                $"fts4(Ix, Hx, {nameof(IGlyphData.Description)})");

            con.Execute($"CREATE TRIGGER insert_trigger_uni AFTER INSERT ON {nameof(UnicodeGlyphData)} " +
                $"BEGIN INSERT INTO {UNICODE_SEARCH_TABLE}(Ix, Hx, {nameof(IGlyphData.Description)}) " +
                $"VALUES (new.Ix, new.Hx, new.{nameof(IGlyphData.Description)}); END;");
        }
    }
#endif
}
