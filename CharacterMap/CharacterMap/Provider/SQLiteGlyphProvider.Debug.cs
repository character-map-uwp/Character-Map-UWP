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
using CharacterMap.Models;

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
                await PopulateFontAsync<FontAwesomeGlyph>(connection, "FontAwesome.txt").ConfigureAwait(false);
                await PopulateFontAsync<MaterialDesignIconsLegacyGlyph>(connection, "materialdesignicons.txt").ConfigureAwait(false);
                await PopulateFontAsync<MaterialDesignIconsGlyph>(connection, "materialdesignicons5.txt").ConfigureAwait(false);
                await PopulateFontAsync<IcoFontGlyph>(connection, "icofont.txt").ConfigureAwait(false);

                var unicode = await PopulateUnicodeAsync(connection).ConfigureAwait(false);
                await PopulateDingsAsync<WebdingsGlyph>(connection, unicode, "Webdings");
                await PopulateDingsAsync<WingdingsGlyph>(connection, unicode, "Wingdings");
                await PopulateDingsAsync<Wingdings2Glyph>(connection, unicode, "Wingdings2");
                await PopulateDingsAsync<Wingdings3Glyph>(connection, unicode, "Wingdings3");

                using (SQLiteConnection con = new SQLiteConnection(connection))
                {
                    con.Execute($"DROP TRIGGER IF EXISTS insert_trigger_{MDL2_SEARCH_TABLE}");
                    con.Execute($"DROP TRIGGER IF EXISTS insert_trigger_{UNICODE_SEARCH_TABLE}");

                    con.Execute($"INSERT INTO {MDL2_SEARCH_TABLE}({MDL2_SEARCH_TABLE}) VALUES('optimize')");
                    con.Execute($"INSERT INTO {UNICODE_SEARCH_TABLE}({UNICODE_SEARCH_TABLE}) VALUES('optimize')");

                    foreach (SearchTarget target in SearchTarget.KnownTargets)
                    {
                        con.Execute($"DROP TRIGGER IF EXISTS insert_trigger_{target.SearchTable}");
                        con.Execute($"INSERT INTO {target.SearchTable}({target.SearchTable}) VALUES('optimize')");
                    }
                }

                using (SQLiteConnection con = new SQLiteConnection(connection))
                {
                    con.Execute("VACUUM \"main\"");
                    con.Execute("VACUUM \"temp\"");
                    con.Execute("VACUUM");
                }

                _connection = new SQLiteConnection(connection);
            });
        }

        private static string Humanize(string s)
        {
            char prev = char.MinValue;

            StringBuilder sb = new StringBuilder();
            foreach (var c in s)
            {
                if (char.IsLower(prev) && char.IsUpper(c))
                    sb.Append(" ");
                else if ((char.IsPunctuation(c) || char.IsSeparator(c)) && c != ')')
                    sb.Append(" ");
                else if (sb.Length > 0 && char.IsDigit(c) && !char.IsDigit(prev))
                    sb.Append(" ");
                else if(char.IsDigit(prev) && char.IsLetter(c) && (prev != '3' && c != 'D'))
                    sb.Append(" ");

                sb.Append(c);
                prev = c;
            }

            return sb.ToString()
                .Replace("  ", " ")
                .Replace("HWP", "HWP ")
                .Replace("IRM", "IRM ")
                .Replace("NUI", "NUI ")
                .Replace("NUI FP", "NUI FP ")
                .Replace("PPS", "PPS ")
                .Replace("Power Point", "PowerPoint")
                .Replace("SIM", "SIM ")
                .Replace("CRM", "CRM ")
                .Replace("CHT", "CHT ")
                .Replace("AAD", "AAD ")
                .Replace("ATP", "ATP ")
                .Replace("MSN", "MSN ")
                .Replace("e SIM", "eSIM")
                .Replace("MobeSIM", "Mob eSIM")
                .Replace("RTT", "RTT ")
                .Replace("USB", "USB ")
                .Replace("LTE", " LTE ")
                .Replace("QWERTY", "QWERTY ")
                .Replace("Qand A", "Q and A ")
                .Replace("  ", " ")
                .Trim();
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
                    var hex = ((int)e).ToString("X4");
                    if (!datas.Any(d => d.code.Equals(hex)))
                        datas.Add((hex, name));
                }

                /* read fabric mdl2 listing */
                var fabric = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/FabricMDL2.txt")).AsTask().ConfigureAwait(false);
                using (var stream = await fabric.OpenStreamForReadAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                {
                    string[] parts;
                    while (!reader.EndOfStream)
                    {
                        parts = reader.ReadLine().Split(" ", StringSplitOptions.None);
                        if (!datas.Any(d => d.code.Equals(parts[0], StringComparison.OrdinalIgnoreCase)))
                            datas.Add((parts[0], parts[1]));
                    }
                }

                /* read manually created full mdl2 listings */
                var manual = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/FullMDL2ManualList.txt")).AsTask().ConfigureAwait(false);
                using (var stream = await manual.OpenStreamForReadAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                {
                    string[] parts;
                    while (!reader.EndOfStream)
                    {
                        parts = reader.ReadLine().Split(" ", StringSplitOptions.None);
                        if (!datas.Any(d => d.code.Equals(parts[0], StringComparison.OrdinalIgnoreCase)))
                            datas.Add((parts[0], parts[1]));
                    }
                }

                var data = datas.Select(d => new MDL2Glyph
                {
                    Description = Humanize(d.name),
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

        private Task<List<UnicodeGlyphData>> PopulateUnicodeAsync(SQLiteConnectionString connection)
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

                        return data;
                    }
                }
            });
        }

        private Task PopulateFontAsync<T>(SQLiteConnectionString connection, string fileName) where T : GlyphDescription, new()
        {
            return Task.Run(async () =>
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/Data/{fileName}")).AsTask().ConfigureAwait(false);
                
                using var c = new SQLiteConnection(connection);
                using var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false);
                using var reader = new StreamReader(stream);

                string[] parts;
                List<T> data = new List<T>();
                while (!reader.EndOfStream)
                {
                    parts = reader.ReadLine().Split(" ", 2, StringSplitOptions.None);

                    string desc = parts[1];
                    string hex = parts[0].ToUpper();
                    int code = Int32.Parse(hex, System.Globalization.NumberStyles.HexNumber);

                    data.Add(new T
                    {
                        Description = desc.Humanize(LetterCasing.Title),
                        UnicodeHex = hex,
                        UnicodeIndex = code
                    });
                }

                c.RunInTransaction(() => c.InsertAll(data.OrderBy(d => d.UnicodeIndex).ToList()));
            });
        }

        private async Task PopulateDingsAsync<T>(SQLiteConnectionString connection, List<UnicodeGlyphData> unicode, string fileName) where T : GlyphDescription, new()
        {
            /* read material design icon */
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/Data/{fileName}.txt")).AsTask().ConfigureAwait(false);
            using (var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream))
            {
                string[] parts;
                List<T> data = new List<T>();
                while (!reader.EndOfStream)
                {
                    parts = reader.ReadLine().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int code = Convert.ToInt32(parts[0], 10);
                    string hex = code.ToString("x4").ToUpper();
                    data.Add(new T
                    {
                        Description = unicode.First(u => u.UnicodeIndex == Convert.ToInt32(parts[1], 10)).Description,
                        UnicodeHex = hex,
                        UnicodeIndex = code
                    });

                    // Ding fonts are a little nuts and have duplicate Unicode ranges that remap all the glyphs 
                    // from the original Dings range to the appropriate Unicode range... about 60,000+ places away.
                    // Whilst still also keeping the original mappings.
                    string hex2 = $"F{hex.Remove(0, 1)}";
                    int code2 = Int32.Parse(hex2, System.Globalization.NumberStyles.HexNumber);

                    data.Add(new T
                    {
                        Description = unicode.First(u => u.UnicodeIndex == Convert.ToInt32(parts[1], 10)).Description,
                        UnicodeHex = hex2,
                        UnicodeIndex = code2
                    });
                }

                if (fileName == "Wingdings")
                {
                    data.Add(new T
                    {
                        Description = "Windows Flag",
                        UnicodeHex = 255.ToString("X4").ToUpper(),
                        UnicodeIndex = 255
                    });

                    data.Add(new T
                    {
                        Description = "Windows Flag",
                        UnicodeHex = 61695.ToString("X4").ToUpper(),
                        UnicodeIndex = 61695
                    });
                }

                using (var c = new SQLiteConnection(connection))
                {
                    c.RunInTransaction(() => c.InsertAll(data.OrderBy(d => d.UnicodeIndex).ToList()));
                }
            }
        }

        private static void PrepareDatabase(SQLiteConnection con)
        {
            con.CreateTable<MDL2Glyph>();
            con.CreateTable<UnicodeGlyphData>();

            foreach (SearchTarget target in SearchTarget.KnownTargets)
                con.CreateTable(target.TargetType);

            /* MDL2 SEARCH */
            CreateSearchTable(con, MDL2_SEARCH_TABLE, nameof(MDL2Glyph));

            /* UNICODE SEARCH */
            CreateSearchTable(con, UNICODE_SEARCH_TABLE, nameof(UnicodeGlyphData));

            /* GENERIC ICON FONTS */
            foreach (SearchTarget target in SearchTarget.KnownTargets)
                CreateSearchTable(con, target.SearchTable, target.TargetType.Name);
        }

        private static void CreateSearchTable(SQLiteConnection con, string table, string insertSource)
        {
            con.Execute($"CREATE VIRTUAL TABLE {table} USING " +
                $"fts4(Ix, Hx, {nameof(IGlyphData.Description)}, matchinfo=fts3)");

            con.Execute($"CREATE TRIGGER insert_trigger_{table} AFTER INSERT ON {insertSource} " +
                $"BEGIN INSERT INTO {table}(Ix, Hx, {nameof(IGlyphData.Description)}) " +
                $"VALUES (new.Ix, new.Hx, new.{nameof(IGlyphData.Description)}); END;");
        }
    }
#endif
}
