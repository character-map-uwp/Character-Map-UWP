using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using CharacterMap.Wpf.Services;
using CharacterMap.Wpf.ViewModels;

internal static class AdvancedFontTests
{
    public static void Run(MainWindowViewModel vm, Action<bool, string> check)
    {
        byte[] sfnt = File.ReadAllBytes(vm.CurrentFace!.FontUri.LocalPath);
        foreach (bool compress in new[] { false, true })
        {
            byte[] woff = Encode(sfnt, compress);
            byte[] decoded = WebFontDecoder.Decode(woff);
            // Directory search fields, table order and checkSumAdjustment may legitimately change.
            check(U16(decoded, 4) == U16(sfnt, 4), "WOFF preserves table count");
            for (int i = 0; i < U16(sfnt, 4); i++)
            {
                int p = 12 + i * 16;
                int originalOffset = (int)U32(sfnt, p + 8), offset = (int)U32(decoded, p + 8), size = (int)U32(sfnt, p + 12);
                byte[] originalTable = sfnt.AsSpan(originalOffset, size).ToArray(), table = decoded.AsSpan(offset, size).ToArray();
                if (U32(sfnt, p) == 0x68656164) { originalTable.AsSpan(8, 4).Clear(); table.AsSpan(8, 4).Clear(); }
                check(originalTable.SequenceEqual(table), "WOFF preserves original table bytes");
            }
            uint sum = 0;
            for (int i = 0; i < decoded.Length; i += 4) sum = unchecked(sum + U32(decoded, i));
            check(sum == 0xB1B0AFBA, "reconstructed OpenType checksum");
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".woff");
            try
            {
                File.WriteAllBytes(path, woff);
                var catalog = new FontCatalogService();
                var fonts = catalog.LoadFontCollection(path);
                check(fonts.Count > 0 && fonts[0].SourcePath == path, "WOFF imports with original source path");
                check(fonts[0].GlyphTypeface.CharacterToGlyphMap.SequenceEqual(vm.CurrentFace.CharacterToGlyphMap), "WOFF imported character map matches source");
                check(ReferenceEquals(fonts, catalog.LoadFontCollection(path)), "repeat WOFF import reuses decoded font");
                check(File.ReadAllBytes(path).SequenceEqual(woff), "WOFF source remains unchanged");
            }
            finally { File.Delete(path); }
            byte[] bad = (byte[])woff.Clone(); W32(bad, 16, uint.MaxValue);
            Reject(() => WebFontDecoder.Decode(bad), check, "reject oversized WOFF output");
            bad = (byte[])woff.Clone(); W32(bad, 48, 0);
            Reject(() => WebFontDecoder.Decode(bad), check, "reject overlapping WOFF table");
            bad = (byte[])woff.Clone(); bad[60] ^= 1;
            Reject(() => WebFontDecoder.Decode(bad), check, "reject WOFF checksum mismatch");
            Reject(() => WebFontDecoder.Decode(woff[..^1]), check, "reject truncated WOFF");
            if (compress)
            {
                int table = Enumerable.Range(0, U16(woff, 12)).Select(i => 44 + i * 20).First(p => U32(woff, p + 8) < U32(woff, p + 12));
                bad = (byte[])woff.Clone(); W32(bad, table + 12, U32(bad, table + 12) - 1);
                Reject(() => WebFontDecoder.Decode(bad), check, "reject decompression beyond declared table size");
            }
            bad = (byte[])woff.Clone(); W32(bad, 0, 0x774F4632);
            Reject(() => WebFontDecoder.Decode(bad), check, "explicit WOFF2 rejection");
        }
        var metadata = new byte[84];
        W32(metadata, 0, 0x00010000); W16(metadata, 4, 2);
        W32(metadata, 12, 0x434F4C52); W32(metadata, 20, 44); W32(metadata, 24, 4);
        W32(metadata, 28, 0x66766172); W32(metadata, 36, 48); W32(metadata, 40, 36);
        W16(metadata, 48, 1); W16(metadata, 52, 16); W16(metadata, 54, 2); W16(metadata, 56, 1); W16(metadata, 58, 20);
        W32(metadata, 64, 0x736C6E74); W32(metadata, 68, unchecked((uint)(-15 * 65536))); W32(metadata, 76, 10 * 65536);
        var info = OpenTypeMetadata.Parse(metadata);
        check(info.ColorTables.Contains("COLR") && info.Axes.Single() == new VariationAxis("slnt", -15, 0, 10), "color detection and signed fvar axis range");
        byte[] malformed = (byte[])metadata.Clone(); W32(malformed, 72, 20 * 65536);
        Reject(() => OpenTypeMetadata.Parse(malformed), check, "reject invalid variable default");
        Reject(() => OpenTypeMetadata.Parse(metadata[..^1]), check, "reject truncated axis table");
        W32(metadata, 0, 0x74746366);
        check(OpenTypeMetadata.Parse(metadata).Unavailable != null, "collection does not claim another face's capabilities");
        check(MainWindowViewModel.IsFontFile("font.WOFF") && MainWindowViewModel.IsFontFile("font.WOFF2"), "web fonts recognized by drop and folder import");
        string unsupported = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".woff2");
        try
        {
            File.WriteAllBytes(unsupported, "wOF2"u8.ToArray());
            var selected = vm.SelectedFont;
            vm.ImportFiles([unsupported]);
            check(vm.SelectedFont == selected && vm.StatusText.Contains("WOFF2") && vm.StatusText.Contains("TTF/OTF"), "unsupported import preserves selection and gives actionable reason");
        }
        finally { File.Delete(unsupported); }
        var previousFont = vm.SelectedFont;
        var previousVariant = vm.SelectedVariant;
        vm.SelectedFont = vm.AllFonts.First(f => f.DisplayName == "Segoe UI Emoji");
        check(OpenTypeMetadata.ForFace(vm.CurrentFace!).ColorTables.Count > 0 && vm.FontSupportSummary.Contains("单色"), "installed color font displays compatibility notice");
        vm.SelectedFont = previousFont; vm.SelectedVariant = previousVariant;
        check(vm.FontSupportSummary.Length == 0, "compatibility notice clears after switching to ordinary font");
    }

    private static void Reject(Action action, Action<bool, string> check, string name)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { check(true, name); return; }
        check(false, name);
    }

    private static byte[] Encode(byte[] sfnt, bool compress)
    {
        int count = U16(sfnt, 4), total = 12 + count * 16;
        using var output = new MemoryStream();
        output.SetLength(44 + count * 20); output.Position = output.Length;
        var directory = new byte[count * 20];
        for (int i = 0; i < count; i++)
        {
            int p = 12 + i * 16, length = (int)U32(sfnt, p + 12);
            var raw = sfnt.AsSpan((int)U32(sfnt, p + 8), length).ToArray();
            byte[] payload = raw;
            if (compress)
            {
                using var packed = new MemoryStream();
                using (var z = new ZLibStream(packed, CompressionLevel.SmallestSize, true)) z.Write(raw);
                if (packed.Length < raw.Length) payload = packed.ToArray();
            }
            int d = i * 20;
            W32(directory, d, U32(sfnt, p)); W32(directory, d + 4, (uint)output.Position);
            W32(directory, d + 8, (uint)payload.Length); W32(directory, d + 12, (uint)length); W32(directory, d + 16, U32(sfnt, p + 4));
            output.Write(payload);
            while (output.Position % 4 != 0) output.WriteByte(0);
            total += (length + 3) & ~3;
        }
        byte[] bytes = output.ToArray();
        W32(bytes, 0, 0x774F4646); W32(bytes, 4, U32(sfnt, 0)); W32(bytes, 8, (uint)bytes.Length);
        W16(bytes, 12, count); W32(bytes, 16, (uint)total); directory.CopyTo(bytes, 44);
        return bytes;
    }
    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p));
    private static ushort U16(byte[] b, int p) => BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(p));
    private static void W32(byte[] b, int p, uint n) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(p), n);
    private static void W16(byte[] b, int p, int n) => BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(p), (ushort)n);
}
