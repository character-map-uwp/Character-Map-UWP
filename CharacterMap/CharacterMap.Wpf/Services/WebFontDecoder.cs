using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace CharacterMap.Wpf.Services;

/// <summary>Bounded WOFF 1 decoder. WOFF2 transforms require a separate, audited decoder.</summary>
public static class WebFontDecoder
{
    public static byte[] Decode(byte[] input)
    {
        if (input.Length > FontBinary.MaximumSize) throw new InvalidDataException("WOFF 文件超过 64 MiB 限制。");
        var r = new FontBinary(input);
        if (r.U32(0) == 0x774F4632) throw new NotSupportedException("WOFF2 解码尚未移植，请使用原始 TTF/OTF 文件。");
        r.Range(0, 44);
        if (r.U32(0) != 0x774F4646 || r.Length(8) != input.Length || r.U16(14) != 0)
            throw new InvalidDataException("WOFF 文件头无效。");
        uint flavor = r.U32(4);
        if (flavor != 0x00010000 && flavor != 0x4F54544F) throw new NotSupportedException("仅支持 TrueType/OpenType WOFF 字体。");
        int count = r.U16(12), total = r.Length(16);
        if (count == 0 || count > 4095 || total < 12 + count * 16) throw new InvalidDataException("WOFF 字体表数量或大小无效。");
        r.Range(44, count * 20);
        var output = new byte[total];
        void W16(int p, int v) => BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(p), checked((ushort)v));
        void W32(int p, uint v) => BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(p), v);
        W32(0, flavor); W16(4, count);
        int power = 1, selector = 0;
        while (power * 2 <= count) { power *= 2; selector++; }
        W16(6, power * 16); W16(8, selector); W16(10, count * 16 - power * 16);
        int destination = 12 + count * 16, head = -1;
        uint previousTag = 0;
        var ranges = new List<(int Start, int End)> { (0, 44 + count * 20) };
        void AddRange(int offset, int length)
        {
            r.Range(offset, length);
            if (offset % 4 != 0 || ranges.Any(v => offset < v.End && offset + length > v.Start))
                throw new InvalidDataException("WOFF 数据区重叠或未对齐。");
            ranges.Add((offset, offset + length));
        }
        for (int i = 0; i < count; i++)
        {
            int p = 44 + i * 20;
            uint tag = r.U32(p);
            int source = r.Length(p + 4), compressed = r.Length(p + 8), original = r.Length(p + 12);
            if ((i > 0 && tag <= previousTag) || original == 0 || compressed == 0 || compressed > original)
                throw new InvalidDataException("WOFF 表目录无效。");
            previousTag = tag;
            AddRange(source, compressed);
            int padded = (original + 3) & ~3;
            if (destination > total - padded) throw new InvalidDataException("WOFF 解压大小不一致。");
            if (compressed == original) input.AsSpan(source, original).CopyTo(output.AsSpan(destination));
            else
            {
                using var segment = new MemoryStream(input, source, compressed, false);
                using var zlib = new ZLibStream(segment, CompressionMode.Decompress);
                zlib.ReadExactly(output.AsSpan(destination, original));
                if (zlib.ReadByte() != -1) throw new InvalidDataException("WOFF 表解压后超出声明大小。");
            }
            if (tag == 0x68656164)
            {
                if (original < 54) throw new InvalidDataException("WOFF head 表截断。");
                head = destination;
                W32(head + 8, 0);
            }
            if (Checksum(output.AsSpan(destination, padded)) != r.U32(p + 16)) throw new InvalidDataException("WOFF 表校验失败。");
            int directory = 12 + i * 16;
            W32(directory, tag); W32(directory + 4, r.U32(p + 16));
            W32(directory + 8, (uint)destination); W32(directory + 12, (uint)original);
            destination += padded;
        }
        // Metadata and private data are not interpreted or extracted, but validate their ranges.
        int metadata = r.Length(24), metadataLength = r.Length(28), metadataOriginal = r.Length(32);
        if (metadata == 0 ? metadataLength != 0 || metadataOriginal != 0 : metadataLength == 0 || metadataOriginal == 0)
            throw new InvalidDataException("WOFF 元数据范围无效。");
        if (metadata != 0) AddRange(metadata, metadataLength);
        int privateOffset = r.Length(36), privateLength = r.Length(40);
        if ((privateOffset == 0) != (privateLength == 0)) throw new InvalidDataException("WOFF 私有数据范围无效。");
        if (privateOffset != 0) AddRange(privateOffset, privateLength);
        if (destination != total || head < 0) throw new InvalidDataException("WOFF 总大小或 head 表无效。");
        W32(head + 8, unchecked(0xB1B0AFBAu - Checksum(output)));
        return output;
    }

    private static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4) sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data[i..]));
        return sum;
    }
}
