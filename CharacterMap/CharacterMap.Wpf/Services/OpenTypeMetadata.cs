using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Runtime.CompilerServices;

namespace CharacterMap.Wpf.Services;

public sealed record VariationAxis(string Tag, double Minimum, double Default, double Maximum);

public sealed record OpenTypeMetadata(IReadOnlyList<string> ColorTables, IReadOnlyList<VariationAxis> Axes, string? Unavailable = null)
{
    private static readonly ConditionalWeakTable<GlyphTypeface, OpenTypeMetadata> Cache = new();
    public string Description => Unavailable ??
        (ColorTables.Count == 0 ? "未检测到彩色字形表。" : $"彩色字形表：{string.Join(", ", ColorTables)}。字符预览支持 COLR/CPAL 基础颜色图层；其他彩色格式回退为可用的单色轮廓。导出和打印仍使用单色轮廓。") +
        (Axes.Count == 0 ? "" : "\n检测到可变字体：当前使用 WPF 提供的字体实例，尚不支持自定义轴值。\n" +
            string.Join("\n", Axes.Select(a => FormattableString.Invariant($"{a.Tag}：{a.Minimum:0.###} ～ {a.Maximum:0.###}（默认 {a.Default:0.###}）"))));

    public static OpenTypeMetadata ForFace(GlyphTypeface face) => Cache.GetValue(face, f =>
    {
        try
        {
            if (!f.FontUri.IsFile) return new([], [], "无法检查此字体来源的高级特性。");
            return Parse(FontBinary.ReadFile(f.FontUri.LocalPath));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new([], [], "高级字体元数据无法读取；当前仍使用 WPF 单色轮廓。"); }
    });

    public static OpenTypeMetadata Parse(byte[] data)
    {
        var r = new FontBinary(data);
        uint signature = r.U32(0);
        // WPF does not expose the collection face index. Do not show another face's axes.
        if (signature == 0x74746366) return new([], [], "字体集合：尚未检测当前字面的彩色表和可变轴。当前使用 WPF 单色轮廓，不能调整轴值。");
        if (signature != 0x00010000 && signature != 0x4F54544F) throw new InvalidDataException("不是支持的 OpenType 字体。");
        int count = r.U16(4);
        if (count == 0 || count > 4095) throw new InvalidDataException("字体表数量无效。");
        r.Range(12, count * 16);
        var tables = new Dictionary<string, byte[]>();
        for (int i = 0; i < count; i++)
        {
            int p = 12 + i * 16;
            string tag = r.Tag(p);
            int offset = r.Length(p + 8), length = r.Length(p + 12);
            r.Range(offset, length);
            if (!tables.TryAdd(tag, tag is "fvar" ? data.AsSpan(offset, length).ToArray() : []))
                throw new InvalidDataException("字体表重复。");
        }
        var axes = new List<VariationAxis>();
        if (tables.TryGetValue("fvar", out var variation))
        {
            var f = new FontBinary(variation);
            f.Range(0, 16);
            if (f.U16(0) != 1 || f.U16(2) != 0) throw new InvalidDataException("不支持的可变轴表版本。");
            int start = f.U16(4), axisCount = f.U16(8), size = f.U16(10);
            if (start < 16 || size < 20 || axisCount > 256) throw new InvalidDataException("可变轴表无效。");
            f.Range(start, axisCount * size);
            var tags = new HashSet<string>();
            for (int i = 0; i < axisCount; i++)
            {
                int p = start + i * size;
                var axis = new VariationAxis(f.Tag(p), f.Fixed(p + 4), f.Fixed(p + 8), f.Fixed(p + 12));
                if (axis.Minimum > axis.Default || axis.Default > axis.Maximum || !tags.Add(axis.Tag))
                    throw new InvalidDataException("可变轴范围或标签无效。");
                axes.Add(axis);
            }
        }
        return new(new[] { "COLR", "CPAL", "CBDT", "CBLC", "sbix", "SVG " }.Where(tables.ContainsKey).ToArray(), axes.AsReadOnly());
    }
}

internal sealed class FontBinary(byte[] data)
{
    internal const int MaximumSize = 64 * 1024 * 1024;
    internal static byte[] ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumSize) throw new InvalidDataException("字体文件超过 64 MiB 的安全读取限制。");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
    internal void Range(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length) throw new InvalidDataException("字体数据截断或偏移无效。");
    }
    internal ushort U16(int p) { Range(p, 2); return BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p)); }
    internal uint U32(int p) { Range(p, 4); return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p)); }
    internal int Length(int p) { uint n = U32(p); return n <= MaximumSize ? (int)n : throw new InvalidDataException("字体数据超过安全大小限制。"); }
    internal string Tag(int p) { Range(p, 4); return Encoding.ASCII.GetString(data, p, 4); }
    internal double Fixed(int p) => unchecked((int)U32(p)) / 65536d;
}
