using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CharacterMap.Wpf.Services;

public sealed record UnicodeBlock(int Start, int End, string Name)
{
    public override string ToString() => Name;
}

public static class UnicodeDataService
{
    private static readonly Lazy<Dictionary<int, string>> Names = new(ReadNames);
    public static IReadOnlyList<UnicodeBlock> Blocks { get; } = ReadBlocks();

    public static string GetName(int codePoint)
    {
        if (Names.Value.TryGetValue(codePoint, out var name)) return name;
        var block = GetBlock(codePoint);
        if (block.Name.Contains("CJK Unified", StringComparison.Ordinal)) return $"CJK UNIFIED IDEOGRAPH-{codePoint:X4}";
        if (codePoint is >= 0xAC00 and <= 0xD7A3)
        {
            string[] l = ["G", "GG", "N", "D", "DD", "R", "M", "B", "BB", "S", "SS", "", "J", "JJ", "C", "K", "T", "P", "H"];
            string[] v = ["A", "AE", "YA", "YAE", "EO", "E", "YEO", "YE", "O", "WA", "WAE", "OE", "YO", "U", "WEO", "WE", "WI", "YU", "EU", "YI", "I"];
            string[] t = ["", "G", "GG", "GS", "N", "NJ", "NH", "D", "L", "LG", "LM", "LB", "LS", "LT", "LP", "LH", "M", "B", "BS", "S", "SS", "NG", "J", "C", "K", "T", "P", "H"];
            int s = codePoint - 0xAC00;
            return "HANGUL SYLLABLE " + l[s / 588] + v[s % 588 / 28] + t[s % 28];
        }
        return $"U+{codePoint:X4} · {block.Name}";
    }

    public static UnicodeBlock GetBlock(int codePoint) => Blocks.FirstOrDefault(b => codePoint >= b.Start && codePoint <= b.End)
        ?? new UnicodeBlock(codePoint, codePoint, "Unassigned / Private Use");

    private static Dictionary<int, string> ReadNames()
    {
        using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("UnicodeData.txt")!);
        var names = new Dictionary<int, string>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split(';');
            if (!fields[1].StartsWith('<')) names[int.Parse(fields[0], NumberStyles.HexNumber)] = fields[1];
        }
        return names;
    }

    private static IReadOnlyList<UnicodeBlock> ReadBlocks()
    {
        using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("UnicodeRanges.txt")!);
        return Regex.Matches(reader.ReadToEnd(), "new\\(0x([0-9A-Fa-f]+), 0x([0-9A-Fa-f]+), \"([^\"]+)\"\\)")
            .Select(m => new UnicodeBlock(int.Parse(m.Groups[1].Value, NumberStyles.HexNumber), int.Parse(m.Groups[2].Value, NumberStyles.HexNumber), m.Groups[3].Value))
            .OrderBy(b => b.Start).ToArray();
    }
}
