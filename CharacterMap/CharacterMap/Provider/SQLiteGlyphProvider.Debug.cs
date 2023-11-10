// Ignore Spelling: MDL


#if DEBUG && GENERATE_DATABASE
using Humanizer;
using System.Globalization;
#endif

namespace CharacterMap.Provider;


/* 
 * These are DEV time only methods to create a new database when data changes.
 * After creating a new database copy it to the Assets/Data/ folder to ship it with
 * the app
 */
#if DEBUG && GENERATE_DATABASE

/// <summary>
/// Glyphs with prefixes ranging from E0- to E5- are legacy, so we return them at the end of search results
/// See https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font#icon-list
/// </summary>
public class MDL2Comparer : IComparer<int>
{
    const int min = 0xE000;
    const int max = 0xE5FF;

    public int Compare(int x, int y)
    {
        if (IsDeprecated(x))
            x += 2_000_000;
        if (IsDeprecated(y))
            y += 2_000_000;

        return x - y;
    }

    public bool IsDeprecated(MDL2Glyph g)
    {
        return g.UnicodeIndex >= min && g.UnicodeIndex <= max;
    }

    public bool IsDeprecated(int index)
    {
        return index >= min && index <= max;
    }
}

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

            SQLiteConnectionString connection = new (path);
            using (SQLiteConnection con = new (connection))
            {
                PrepareDatabase(con);
            }

            await PopulateAglfnAsync(connection).ConfigureAwait(false);
            await PopulateMDL2Async(connection).ConfigureAwait(false);
            await PopulateUnihanReadingsAsync(connection).ConfigureAwait(false);

            await PopulateFontAsync<FontAwesomeGlyph>(connection, "FontAwesome.txt").ConfigureAwait(false);

            var unicode = await PopulateUnicodeAsync(connection).ConfigureAwait(false);
            await PopulateDingsAsync<WebdingsGlyph>(connection, unicode, "Webdings");
            await PopulateDingsAsync<WingdingsGlyph>(connection, unicode, "Wingdings");
            await PopulateDingsAsync<Wingdings2Glyph>(connection, unicode, "Wingdings2");
            await PopulateDingsAsync<Wingdings3Glyph>(connection, unicode, "Wingdings3");

#if USE_FTS
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
#endif

            using (SQLiteConnection con = new(connection))
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

    private Task PopulateAglfnAsync(SQLiteConnectionString connection)
    {
        return Task.Run(async () =>
        {
            var fabric = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/glyphlist.txt")).AsTask().ConfigureAwait(false);
            using var stream = await fabric.OpenStreamForReadAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            string[] parts;
            int line = 1;
            while (line < 44)
            {
                reader.ReadLine();
                line++;
            }

            List<AdobeGlyphListMapping> mappings = new List<AdobeGlyphListMapping>();

            while (!reader.EndOfStream)
            {
                parts = reader.ReadLine().Split(";", StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var indexes = parts[1].Split(" ");
                    var mapping = new AdobeGlyphListMapping
                    {
                        Value = parts[0],
                        UnicodeIndex = int.Parse(indexes[0], System.Globalization.NumberStyles.HexNumber),
                        UnicodeIndex2 = indexes.Length > 1 ? int.Parse(indexes[1], System.Globalization.NumberStyles.HexNumber) : 0,
                        UnicodeIndex3 = indexes.Length > 2 ? int.Parse(indexes[2], System.Globalization.NumberStyles.HexNumber) : 0,
                        UnicodeIndex4 = indexes.Length > 3 ? int.Parse(indexes[3], System.Globalization.NumberStyles.HexNumber) : 0
                    };

                    mappings.Add(mapping);
                }
            }

            using var c = new SQLiteConnection(connection);
            c.RunInTransaction(() => { c.InsertAll(mappings); });
        });
    }

    private Task PopulateUnihanReadingsAsync(SQLiteConnectionString connection)
    {
        return Task.Run(async () =>
        {
            var fabric = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/Unihan_Readings.txt")).AsTask().ConfigureAwait(false);
            using var stream = await fabric.OpenStreamForReadAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            string[] parts;
            string line;

            List<UnihanReading> mappings = new();

            while (!reader.EndOfStream)
            {
                line = reader.ReadLine();
                if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                    continue;

                parts = line.Split("\t", StringSplitOptions.None);
                if (parts.Length == 3)
                {
                    var desc = parts[2];

                    if (parts[1] != "kDefinition" && desc.Contains(":"))
                    {
                        if (parts[1] == "kHangul")
                        {
                            // e.g. 양:0E -> 양 (0E)
                            desc = $"{desc.Replace(":", " (")})";
                        }
                        else
                        {
                            // Special parsing for for Hanyu readings
                            // e.g. converts '31914.110:yáng'
                            //            to 'yáng (31914.110)'
                            //
                            //      converts '0069.080:biāo 1008.081:sháo'
                            //            to 'biāo (0069.080), sháo (1008.081)'

                            string o = desc;
                            desc = string.Empty;

                            if (o.Contains(" "))
                            {
                                var op = o.Split(" ");
                                foreach (var opart in op)
                                    Append(opart.Split(":"));
                            }
                            else
                                Append(o.Split(":"));

                            void Append(string[] p)
                            {
                                if (!string.IsNullOrWhiteSpace(desc))
                                    desc += ", ";
                                desc += $"{p[1]} ({p[0]})";
                            }
                        }
                    }
                    else if (desc.IndexOf("(") is int idx && idx > 0 && desc[idx - 1] != ' ')
                    {
                        // Adds a space infront of brackets
                        // e.g. 'yáng(179)' bcomes 'yáng (179)'
                        desc = desc.Insert(idx, " ");
                    }
                    else if (parts[1] != "kDefinition")
                        desc = desc.ToLower();

                    var mapping = new UnihanReading(
                        index: int.Parse(parts[0].Remove(0,2), NumberStyles.HexNumber),
                        type: Enum.Parse<UnihanFieldType>(parts[1].Remove(0, 1)),
                        description: desc.Replace(",", ", ").Replace("  ", " "));

                    mappings.Add(mapping);
                }
            }

            // Order everything into our desired order
            // so when we load data we don't have to sort
            mappings = mappings
                .GroupBy(m => m.Index)
                .OrderBy(g => g.Key)
                .SelectMany(g => g.OrderBy(r => r.Type))
                .ToList();

            using var c = new SQLiteConnection(connection);
            c.RunInTransaction(() => { c.InsertAll(mappings); });
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
                var hex = ((int)e).ToString("X4");
                if (!datas.Any(d => d.code.Equals(hex)))
                    datas.Add((hex, name));
            }

            /* read fabric mdl2 listing */
            var fabric = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/FabricMDL2.txt")).AsTask().ConfigureAwait(false);
            using (var stream = await fabric.OpenStreamForReadAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream))
            {
                HashSet<String> ignore = new() // these conflict with system MDL2 icons
                {
                    "E614", "E615", "E616", "E617", "E618",
                    "E65B", "E62E", "E65B", "E66C",
                    "E670", "E671", "E672", "E673", "E674",
                    "E678", "E67A",
                };

                string[] parts;
                while (!reader.EndOfStream)
                {
                    parts = reader.ReadLine().Split(" ", StringSplitOptions.None);
                    if (ignore.Contains(parts[0]) is false &&
                        !datas.Any(d => d.code.Equals(parts[0], StringComparison.OrdinalIgnoreCase)))
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

            var comparer = new MDL2Comparer();

            var data = datas.Select(d => new MDL2Glyph
            {
                Description = $"{Humanize(d.name)}{(comparer.IsDeprecated(int.Parse(d.code, NumberStyles.HexNumber)) ? " (Deprecated)" : string.Empty)}",
                UnicodeIndex = int.Parse(d.code, NumberStyles.HexNumber),
                UnicodeHex = d.code
            }).OrderBy(g => g.UnicodeIndex, comparer).ToList();

            using (SQLiteConnection c = new (connection))
            {
                c.RunInTransaction(() => c.InsertAll(data));
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

                        // Skip things that announce start/end of ranges
                        if (desc.EndsWith("First>") || desc.EndsWith("Last>"))
                            continue;

                        // Skip Ideograph labels
                        if (desc.StartsWith("CJK COMPATIBILITY IDEOGRAPH-"))
                            continue;

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
            List<T> data = new ();
            while (!reader.EndOfStream)
            {
                parts = reader.ReadLine().Split(" ", 2, StringSplitOptions.None);

                string desc = parts[1];
                string hex = parts[0].ToUpper();
                int code = Int32.Parse(hex, NumberStyles.HexNumber);

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
                int code2 = Int32.Parse(hex2, NumberStyles.HexNumber);

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

            using (SQLiteConnection c = new (connection))
            {
                c.RunInTransaction(() => c.InsertAll(data.OrderBy(d => d.UnicodeIndex).ToList()));
            }
        }
    }

    private static void PrepareDatabase(SQLiteConnection con)
    {
        con.CreateTable<MDL2Glyph>();
        con.CreateTable<UnicodeGlyphData>();
        con.CreateTable<AdobeGlyphListMapping>();
        con.CreateTable<UnihanReading>();

        foreach (SearchTarget target in SearchTarget.KnownTargets)
            con.CreateTable(target.TargetType);

#if USE_FTS
        // Create Fast-Text-Search tables.
        /* MDL2 SEARCH */
        CreateSearchTable(con, MDL2_SEARCH_TABLE, nameof(MDL2Glyph));

        /* UNICODE SEARCH */
        CreateSearchTable(con, UNICODE_SEARCH_TABLE, nameof(UnicodeGlyphData));

        /* GENERIC ICON FONTS */
        foreach (SearchTarget target in SearchTarget.KnownTargets)
            CreateSearchTable(con, target.SearchTable, target.TargetType.Name);
#endif
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
