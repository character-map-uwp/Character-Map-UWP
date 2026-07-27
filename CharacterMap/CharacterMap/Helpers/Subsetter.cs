using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using Microsoft.Graphics.Canvas.Text;

namespace CharacterMap.Helpers;

public record SubsetOptions(
    string DesiredName,
    IList<FontGlyph> Characters,
    StorageFile OutputFile = null,
    string DesiredVersion = "Version 1.00",
    bool generatePreviewString = true);

public record FontGlyphMetrics(
    float Scale = 1f,
    float OffsetX = 0f,
    float OffsetY = 0f,
    float CustomAdvanceWidth = 0f);



[ObservableObject]
public partial class FontGlyph(
    CMFontFace fontFace, 
    Character character, 
    FontGlyphMetrics metrics = default,
    CanvasGeometry CustomGeometry = null, 
    string CustomImagePath = null)
{

    public CMFontFace FontFace { get; } = fontFace;
    public Character Character { get; set; } = character;
    public FontGlyphMetrics Metrics { get; set; } = metrics ?? new ();
    public CanvasGeometry CustomGeometry { get; } = CustomGeometry;
    public string CustomImagePath { get; } = CustomImagePath;


    public bool IsVirtual => !IsPhysical;
    public bool IsPhysical => FontFace is not null;

    public string Description => IsPhysical 
        ? FontFace.FullName 
        : Localization.Get("ExportSVGGlyphLabel/Text");

    [ObservableProperty] string _glyphName;
}

/// <summary>
/// A basic, MVP font subsetter, design for subsetting or merging icon fonts.
/// It was designed primarily to aid the developement of CharacterMapUWP itself,
/// and tested subsetting Segoe MDL2/Fluent icons, and Google Material Icons.
/// 
/// It supports merging glyphs, re-writing appropriate metadata, and rescaling
/// glyphs from fonts with different metrics to share the share base metrics.
/// 
/// (The first FontFamily listed in the UI is used as the base font file for the 
/// output, so output metrics comes from this file. For Windows UI development,
/// ensure this is a Segoe font.)
/// 
/// Given the MVP status, it does NOT support:
///   - Colour glyph handling
///   - Bitmap glyph handling
///   - Ligatures or other similar characters replacements like OpenType typography, etc
///   - Variable fonts
///   - Glyph names
///   - Composite glyphs
///   - Remapping glyphs to differing codepoints
///   - Language strings other than en-us
///   - Composite glyph structures (composite glyphs are flattened into simple glyphs)
///   - Hinting (hinting tables like cvt, fpgm, prep are discarded)
///   - Advanced layout or kerning (GPOS, GSUB, and kern tables are discarded)
///   - Outputting formats other than TrueType (.ttf) sfnt
///   
/// If these tables exist in the font used as the basis for the output file, 
/// they may cause issues on output. 
/// </summary>
public class FontSubsetter
{
    public const string SUBSETTER_TAG = "CMSV";
    public const string SUBSETTER_SIGNATURE = "CMUWP.Subsetter.v1";

    public static bool IsCreatedBySubsetter(CMFontFace font)
    {
        if (font?.Face is null)
            return false;

        byte[] data = font.Face.GetFontTable(SUBSETTER_TAG);
        return data is not null && Encoding.ASCII.GetString(data).StartsWith(SUBSETTER_SIGNATURE);
    }

    // ---------------------------
    // Big-endian helpers
    // ---------------------------

    static ushort ReadUInt16BE(BinaryReader br)
    {
        byte[] b = br.ReadBytes(2);
        return (ushort)((b[0] << 8) | b[1]);
    }

    static short ReadInt16BE(BinaryReader br)
    {
        byte[] b = br.ReadBytes(2);
        return (short)((b[0] << 8) | b[1]);
    }

    static uint ReadUInt32BE(BinaryReader br)
    {
        byte[] b = br.ReadBytes(4);
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    static void WriteUInt16BE(BinaryWriter bw, ushort v)
    {
        bw.Write((byte)(v >> 8));
        bw.Write((byte)(v & 0xFF));
    }

    static void WriteUInt32BE(BinaryWriter bw, uint v)
    {
        bw.Write((byte)(v >> 24));
        bw.Write((byte)((v >> 16) & 0xFF));
        bw.Write((byte)((v >> 8) & 0xFF));
        bw.Write((byte)(v & 0xFF));
    }

    static void WriteInt16BE(BinaryWriter bw, short v)
    {
        bw.Write((byte)((v >> 8) & 0xFF));
        bw.Write((byte)(v & 0xFF));
    }

    //private struct TableRecord
    //{
    //    public string Tag;
    //    public uint Checksum;
    //    public uint Offset;
    //    public uint Length;
    //}

    private class FontTableCache : IDisposable
    {
        public byte[] Head;
        public byte[] Loca;
        public short IndexToLocFormat = -1;
        public DWriteFontTableSession GlyfSession;

        public void Dispose()
        {
            GlyfSession?.Dispose();
        }
    }

    // ---------------------------
    // Main subsetting function
    // ---------------------------



    public static async Task<StorageFile> CreateSubsetAsync(SubsetOptions opts)
    {
        string desiredFamilyName = opts.DesiredName;
        IList<FontGlyph> characters = opts.Characters;

        if (characters == null || characters.Count == 0)
            throw new ArgumentException("No characters provided");


        // 1. Try to ensure we always have:
        //   - null/default
        //   - carriage return
        //   - space
        uint[] required = [32, 13, 0];
        List<CMFontFace> faces = null;
        CMFontFace fallbackFace = FontFinder.DefaultFont.DefaultVariant;
        foreach (var r in required)
        {
            if (!characters.Any(c => c.Character.UnicodeIndex == r))
            {
                faces ??= characters.Select(c => c.FontFace).Where(f => f != null).Distinct().ToList();
                if (faces.Count == 0)
                    faces.Add(fallbackFace);

                if (faces.Select(f => new FontGlyph(f, f.Characters.FirstOrDefault(c => c.UnicodeIndex == r)))
                         .FirstOrDefault(s => s.Character != null) is { } def)
                    characters.Insert(0, def);
            }
        }


        // 2. Verify there are no clashing unicode indexes. We don't currently attempt to handle to remap
        //    them automatically, so do nothing.
        List<uint> clashingUnicode = characters
            .GroupBy(c => c.Character.UnicodeIndex)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (clashingUnicode.Count > 0)
        {
            Utils.AppendDiagnostics("CFF_MERGE_ERROR.txt", $"Clashing unicode indexes detected: {string.Join(", ", clashingUnicode)}\r\n");
            return null;
        }

        // 3. Collect all the unique font faces used
        List<CMFontFace> uniqueFonts = characters.Where(c => c.IsPhysical).Select(c => c.FontFace).Distinct().ToList();
        Dictionary<CMFontFace, ushort> fontFsTypes = new();

        // 3.1. Try to find the fonttype for each font
        foreach (CMFontFace font in uniqueFonts)
        {
            ushort fsType = 0;
            byte[] tempOs2Data = font.Face.GetFontTable("OS/2");
            if (tempOs2Data != null && tempOs2Data.Length >= 10)
                fsType = (ushort)((tempOs2Data[8] << 8) | tempOs2Data[9]);
            fontFsTypes[font] = fsType;
        }

        // 4. Select template font (the first physical one, or fallback to system default)
        CMFontFace templateFont = characters.FirstOrDefault(c => c.IsPhysical)?.FontFace ?? fallbackFace;
        if (templateFont == null)
            throw new InvalidOperationException("No base font available and default system font could not be loaded.");
        ushort unitsPerEm = templateFont.Face.DesignUnitsPerEm;

        // 4.1. Remove the fallback now so it's metadata doesn't get merged in
        faces.Remove(FontFinder.DefaultFont.DefaultVariant);
        uniqueFonts.Remove(FontFinder.DefaultFont.DefaultVariant);

        // 5. Figure out the final CMAP
        ushort outputNumGlyphs = (ushort)(characters.Count + 1);
        Dictionary<uint, uint> outputCmap = new();
        for (int i = 0; i < characters.Count; i++)
        {
            uint unicode = characters[i].Character.UnicodeIndex;
            outputCmap[unicode] = (uint)(i + 1);
        }

        byte[] newCmapTable = BuildCmapTable(outputCmap);

        // 6. Start writing tables
        Dictionary<CMFontFace, FontTableCache> tableCache = [];
        try
        {
            // 6.1. Build output glyf, loca, and hmtx tables
            byte[] newGlyfData;
            uint[] newLoca = new uint[outputNumGlyphs + 1];
            byte[] newHmtxData;

            ushort maxPointsOfAll = 0;
            ushort maxContoursOfAll = 0;

            using (MemoryStream glyfMs = new())
            {
                using (MemoryStream hmtxMs = new())
                using (BinaryWriter hmtxBw = new(hmtxMs))
                {
                    for (uint gid = 0; gid < outputNumGlyphs; gid++)
                    {
                        newLoca[gid] = (uint)glyfMs.Position;

                        CMFontFace srcFont = null;
                        uint srcGid = 0;
                        float scaleVal = 1f;
                        float offsetX = 0f;
                        float offsetY = 0f;
                        FontGlyph fc = null;

                        if (gid == 0)
                        {
                            srcFont = templateFont;
                            srcGid = 0;
                        }
                        else
                        {
                            fc = characters[(int)gid - 1];
                            srcFont = fc.FontFace;
                            if (srcFont != null)
                            {
                                uint unicode = fc.Character.UnicodeIndex;
                                srcGid = (uint)srcFont.Face.GetGlyphIndice(unicode);
                            }
                            scaleVal = fc.Metrics.Scale;
                            offsetX = fc.Metrics.OffsetX;
                            offsetY = fc.Metrics.OffsetY;
                        }

                        ushort aw = unitsPerEm;
                        short lsb = 0;

                        if (srcFont != null)
                        {
                            // Get metrics directly from DirectWrite (packed)
                            long packedMetrics = srcFont.Face.GetGlyphMetricsPacked((ushort)srcGid);
                            aw = (ushort)(packedMetrics & 0xFFFFFFFF);
                            lsb = (short)(packedMetrics >> 32);
                        }
                        else if (fc != null && fc.CustomGeometry != null)
                        {
                            var bounds = fc.CustomGeometry.ComputeBounds();
                            lsb = (short)Math.Round(bounds.X);
                            aw = (ushort)Math.Round(fc.Metrics.CustomAdvanceWidth > 0 ? fc.Metrics.CustomAdvanceWidth : bounds.Width);
                        }

                        // 1. Scale metrics based on differences in EM-size (design units)
                        // We assume custom SVG geometries (srcFont == null) are normalized to a 1024 unit space by SVGHelper.
                        ushort srcUnits = srcFont?.Face.DesignUnitsPerEm ?? 1024;
                        double emScale = (srcUnits == unitsPerEm) ? 1.0 : (double)unitsPerEm / srcUnits;

                        // 2. Scale and shift based on custom character options
                        double finalScale = emScale * scaleVal;
                        aw = (ushort)Math.Round(aw * finalScale);
                        lsb = (short)Math.Round(lsb * finalScale + offsetX);

                        WriteUInt16BE(hmtxBw, aw);
                        WriteInt16BE(hmtxBw, lsb);

                        // Extract outline
                        byte[] ttfBytes;
                        ushort pts = 0;
                        ushort ctrs = 0;

                        if (srcFont != null)
                        {
                            ttfBytes = ExtractCffGlyphAsTtf(srcFont, unitsPerEm, srcGid, scaleVal, offsetX, offsetY, tableCache, out pts, out ctrs);
                        }
                        else if (fc != null && fc.CustomGeometry != null)
                        {
                            ttfBytes = ExtractGeometryAsTtf(fc.CustomGeometry, (float)finalScale, offsetX, offsetY, out pts, out ctrs);
                        }
                        else
                        {
                            ttfBytes = [];
                        }
                        if (ttfBytes.Length > 0)
                        {
                            glyfMs.Write(ttfBytes, 0, ttfBytes.Length);
                            if (pts > maxPointsOfAll) maxPointsOfAll = pts;
                            if (ctrs > maxContoursOfAll) maxContoursOfAll = ctrs;
                        }

                        // Pad to 4-byte boundary
                        while (glyfMs.Position % 4 != 0)
                        {
                            glyfMs.WriteByte(0);
                        }
                    }

                    newLoca[outputNumGlyphs] = (uint)glyfMs.Position;
                    newGlyfData = glyfMs.ToArray();
                    newHmtxData = hmtxMs.ToArray();
                }
            }

            // 6.2. Rebuild Name table with new copyright and descriptions
            List<CMFontFace> metadataFonts = uniqueFonts
                .GroupBy(f => $"{f.FamilyName} {f.PreferredName}".Trim())
                .Select(g => g.First())
                .ToList();

            string MergeField(CanvasFontInformation info)
            {
                List<string> distinctValues = metadataFonts
                    .Select(f => f.TryGetInfo(info)?.Value ?? string.Empty)
                    .Distinct()
                    .ToList();

                if (distinctValues.Count == 1)
                    return distinctValues[0];

                List<string> lines = new();
                foreach (CMFontFace font in metadataFonts)
                {
                    string val = font.TryGetInfo(info)?.Value;
                    string fontName = $"{font.FamilyName} {font.PreferredName}".Trim();
                    if (!string.IsNullOrEmpty(val))
                        lines.Add($"{fontName}:\n{val}\n");
                    else
                        lines.Add($"{fontName}:\nNot specified\n");
                }
                return string.Join("\n", lines).Trim();
            }

            string finalCopyright = MergeField(CanvasFontInformation.CopyrightNotice);
            string finalTrademark = MergeField(CanvasFontInformation.Trademark);
            string finalManufacturer = MergeField(CanvasFontInformation.Manufacturer);
            string finalDesigner = MergeField(CanvasFontInformation.Designer);
            string finalVendorUrl = MergeField(CanvasFontInformation.FontVendorUrl);
            string finalDesignerUrl = MergeField(CanvasFontInformation.DesignerUrl);
            string finalLicenseDesc = MergeField(CanvasFontInformation.LicenseDescription);
            string finalLicenseUrl = MergeField(CanvasFontInformation.LicenseInfoUrl);

            List<string> fontNames = metadataFonts.Select(f => $"{f.FamilyName} {f.PreferredName} ({f.TryGetInfo(CanvasFontInformation.VersionStrings)?.Value})".Trim()).ToList();
            string firstLine = "";
            if (metadataFonts.Count > 1)
                firstLine = $"This font was created as a merged subset of the following fonts:\n{string.Join("\n", fontNames)}\n";
            else if (metadataFonts.Count == 1)
                firstLine = $"This font is a subset of {fontNames[0]}";
            else
                firstLine = "This font was created from SVG files.";

            List<string> descList = new();
            foreach (CMFontFace font in metadataFonts)
            {
                string ds = font.TryGetInfo(CanvasFontInformation.Description)?.Value;
                if (!string.IsNullOrEmpty(ds))
                    descList.Add($"{font.FamilyName} {font.PreferredName}: {ds}");
            }
            string finalDescription = firstLine;
            if (descList.Count > 0)
                finalDescription = firstLine + "\n\n" + string.Join("\n\n", descList);

            finalDescription = finalDescription.Trim();

            byte[] nameData = templateFont.Face.GetFontTable("name");
            byte[] newNameTable = null;
            if (nameData != null)
            {
                // Automatically generate a preview string
                string finalPreview = null;
                if (opts.generatePreviewString)
                {
                    List<string> previewChars = new();
                    foreach (FontGlyph c in characters)
                    {
                        uint unicode = c.Character.UnicodeIndex;
                        if (unicode < 32 || Char.IsWhiteSpace((char)unicode))
                            continue;

                        try
                        {
                            previewChars.Add(char.ConvertFromUtf32((int)unicode));
                        }
                        catch { }

                        if (previewChars.Count >= 50) // TODO: make this an arg? a const?
                            break;
                    }
                    finalPreview = string.Join(string.Empty, previewChars);
                }


                try
                {
                    newNameTable = RebuildNameTable(
                        nameData,
                        desiredFamilyName,
                        finalCopyright,
                        finalDescription,
                        finalDesigner,
                        finalManufacturer,
                        finalTrademark,
                        finalVendorUrl,
                        finalDesignerUrl,
                        finalLicenseDesc,
                        finalLicenseUrl,
                        opts.DesiredVersion,
                        finalPreview);
                }
                catch
                {
                    newNameTable = nameData;
                }
            }

            // Merge OS/2 embedding rights and ranges
            ushort finalFsType = 0;
            uint r1 = 0;
            uint r2 = 0;
            uint r3 = 0;
            uint r4 = 0;
            uint c1 = 0;
            uint c2 = 0;

            if (uniqueFonts.Count > 0)
            {
                foreach (CMFontFace font in uniqueFonts)
                {
                    finalFsType = GetMostRestrictiveFsType(finalFsType, fontFsTypes[font]);

                    byte[] fontOs2 = font.Face.GetFontTable("OS/2");
                    if (fontOs2 != null)
                    {
                        if (fontOs2.Length >= 58)
                        {
                            uint fr1 = (uint)((fontOs2[42] << 24) | (fontOs2[43] << 16) | (fontOs2[44] << 8) | fontOs2[45]);
                            uint fr2 = (uint)((fontOs2[46] << 24) | (fontOs2[47] << 16) | (fontOs2[48] << 8) | fontOs2[49]);
                            uint fr3 = (uint)((fontOs2[50] << 24) | (fontOs2[51] << 16) | (fontOs2[52] << 8) | fontOs2[53]);
                            uint fr4 = (uint)((fontOs2[54] << 24) | (fontOs2[55] << 16) | (fontOs2[56] << 8) | fontOs2[57]);
                            r1 |= fr1;
                            r2 |= fr2;
                            r3 |= fr3;
                            r4 |= fr4;
                        }
                        if (fontOs2.Length >= 86)
                        {
                            uint fc1 = (uint)((fontOs2[78] << 24) | (fontOs2[79] << 16) | (fontOs2[80] << 8) | fontOs2[81]);
                            uint fc2 = (uint)((fontOs2[82] << 24) | (fontOs2[83] << 16) | (fontOs2[84] << 8) | fontOs2[85]);
                            c1 |= fc1;
                            c2 |= fc2;
                        }
                    }
                }
            }
            else
            {
                // For SVG-only subset, keep fallback font's (Segoe UI) ranges
                byte[] fontOs2 = templateFont.Face.GetFontTable("OS/2");
                if (fontOs2 != null)
                {
                    if (fontOs2.Length >= 58)
                    {
                        r1 = (uint)((fontOs2[42] << 24) | (fontOs2[43] << 16) | (fontOs2[44] << 8) | fontOs2[45]);
                        r2 = (uint)((fontOs2[46] << 24) | (fontOs2[47] << 16) | (fontOs2[48] << 8) | fontOs2[49]);
                        r3 = (uint)((fontOs2[50] << 24) | (fontOs2[51] << 16) | (fontOs2[52] << 8) | fontOs2[53]);
                        r4 = (uint)((fontOs2[54] << 24) | (fontOs2[55] << 16) | (fontOs2[56] << 8) | fontOs2[57]);
                    }
                    if (fontOs2.Length >= 86)
                    {
                        c1 = (uint)((fontOs2[78] << 24) | (fontOs2[79] << 16) | (fontOs2[80] << 8) | fontOs2[81]);
                        c2 = (uint)((fontOs2[82] << 24) | (fontOs2[83] << 16) | (fontOs2[84] << 8) | fontOs2[85]);
                    }
                }
            }

            // Fetch template tables for modification and output
            byte[] headData = templateFont.Face.GetFontTable("head");
            if (headData == null) throw new InvalidDataException("Missing head table in template font");
            headData[50] = 0;
            headData[51] = 1; // 32-bit loca format

            // Try parsing version string to update head.fontRevision
            try
            {
                string cleanVersion = opts.DesiredVersion ?? "Version 1.00";
                if (cleanVersion.StartsWith("Version ", StringComparison.OrdinalIgnoreCase))
                    cleanVersion = cleanVersion.Substring(8).Trim();

                if (double.TryParse(cleanVersion, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double verNum))
                {
                    int major = (int)verNum;
                    int minor = (int)Math.Round((verNum - major) * 65536.0);
                    uint fixedVer = (uint)((major << 16) | (minor & 0xFFFF));

                    headData[4] = (byte)(fixedVer >> 24);
                    headData[5] = (byte)((fixedVer >> 16) & 0xFF);
                    headData[6] = (byte)((fixedVer >> 8) & 0xFF);
                    headData[7] = (byte)(fixedVer & 0xFF);
                }
            }
            catch { }

            byte[] hheaData = templateFont.Face.GetFontTable("hhea");
            if (hheaData == null) throw new InvalidDataException("Missing hhea table in template font");
            hheaData[34] = (byte)(outputNumGlyphs >> 8);
            hheaData[35] = (byte)(outputNumGlyphs & 0xFF);

            // Retrieve and update the OS/2 table of the template font.
            // We modify the table to merge and reflect the combined properties of all source fonts:
            // - fsType (embedding restrictions)
            // - ulUnicodeRange (supported Unicode character ranges)
            // - ulCodePageRange (supported code pages)
            byte[] os2Data = templateFont.Face.GetFontTable("OS/2");
            if (os2Data == null) throw new InvalidDataException("Missing OS/2 table in template font");

            // Update fsType (licensing/embedding settings) at offset 8
            if (os2Data.Length >= 10)
            {
                os2Data[8] = (byte)(finalFsType >> 8);
                os2Data[9] = (byte)(finalFsType & 0xFF);
            }

            // Manually update ulUnicodeRange1-4 (supported Unicode ranges) at offset 42 (16 bytes)
            if (os2Data.Length >= 58)
            {
                os2Data[42] = (byte)(r1 >> 24);
                os2Data[43] = (byte)((r1 >> 16) & 0xFF);
                os2Data[44] = (byte)((r1 >> 8) & 0xFF);
                os2Data[45] = (byte)(r1 & 0xFF);

                os2Data[46] = (byte)(r2 >> 24);
                os2Data[47] = (byte)((r2 >> 16) & 0xFF);
                os2Data[48] = (byte)((r2 >> 8) & 0xFF);
                os2Data[49] = (byte)(r2 & 0xFF);

                os2Data[50] = (byte)(r3 >> 24);
                os2Data[51] = (byte)((r3 >> 16) & 0xFF);
                os2Data[52] = (byte)((r3 >> 8) & 0xFF);
                os2Data[53] = (byte)(r3 & 0xFF);

                os2Data[54] = (byte)(r4 >> 24);
                os2Data[55] = (byte)((r4 >> 16) & 0xFF);
                os2Data[56] = (byte)((r4 >> 8) & 0xFF);
                os2Data[57] = (byte)(r4 & 0xFF);
            }

            // Manually update ulCodePageRange1-2 (supported codepages) at offset 78 (8 bytes)
            if (os2Data.Length >= 86)
            {
                os2Data[78] = (byte)(c1 >> 24);
                os2Data[79] = (byte)((c1 >> 16) & 0xFF);
                os2Data[80] = (byte)((c1 >> 8) & 0xFF);
                os2Data[81] = (byte)(c1 & 0xFF);

                os2Data[82] = (byte)(c2 >> 24);
                os2Data[83] = (byte)((c2 >> 16) & 0xFF);
                os2Data[84] = (byte)((c2 >> 8) & 0xFF);
                os2Data[85] = (byte)(c2 & 0xFF);
            }

            byte[] postData = BuildPostTable(characters, outputNumGlyphs);

            // Rebuild head, hhea, maxp tables
            byte[] maxpData = new byte[32];
            maxpData[1] = 1; // version 1.0 (0x00010000)
            maxpData[4] = (byte)(outputNumGlyphs >> 8);
            maxpData[5] = (byte)(outputNumGlyphs & 0xFF);
            maxpData[6] = (byte)(maxPointsOfAll >> 8);
            maxpData[7] = (byte)(maxPointsOfAll & 0xFF);
            maxpData[8] = (byte)(maxContoursOfAll >> 8);
            maxpData[9] = (byte)(maxContoursOfAll & 0xFF);
            maxpData[15] = 1; // maxZones = 1

            // Rebuild loca data
            byte[] newLocaData;
            using (MemoryStream locaMs = new())
            using (BinaryWriter locaBw = new(locaMs))
            {
                for (int i = 0; i <= outputNumGlyphs; i++)
                    WriteUInt32BE(locaBw, newLoca[i]);
                newLocaData = locaMs.ToArray();
            }

            // Write all tables into the final Stream
            Dictionary<string, byte[]> outputTables = new(StringComparer.Ordinal);
            outputTables["cmap"] = newCmapTable;
            outputTables["glyf"] = newGlyfData;
            outputTables["loca"] = newLocaData;
            outputTables["maxp"] = maxpData;
            outputTables["hhea"] = hheaData;
            outputTables["hmtx"] = newHmtxData;
            outputTables["head"] = headData;
            if (newNameTable != null) outputTables["name"] = newNameTable;
            if (os2Data != null) outputTables["OS/2"] = os2Data;
            if (postData != null) outputTables["post"] = postData;
            outputTables[SUBSETTER_TAG] = Encoding.ASCII.GetBytes(SUBSETTER_SIGNATURE);

            ushort numTables = (ushort)outputTables.Count;
            int maxPower2 = 1;
            while (maxPower2 * 2 <= numTables) maxPower2 *= 2;
            ushort searchRange = (ushort)(maxPower2 * 16);
            ushort entrySelector = (ushort)Math.Log(maxPower2, 2);
            ushort rangeShift = (ushort)(numTables * 16 - searchRange);

            StorageFile file = opts.OutputFile
                ?? await StorageHelper.CreateTempFileAsync($"SS\\{desiredFamilyName}.ttf").AsTask().ConfigureAwait(false);

            using (Stream outStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
            using (BinaryWriter bw = new(outStream))
            {
                outStream.SetLength(0);
                WriteUInt32BE(bw, 0x00010000); // sfntVersion
                WriteUInt16BE(bw, numTables);
                WriteUInt16BE(bw, searchRange);
                WriteUInt16BE(bw, entrySelector);
                WriteUInt16BE(bw, rangeShift);

                long tableRecordsPos = outStream.Position;
                List<string> sortedTags = outputTables.Keys.OrderBy(t => t).ToList();
                foreach (string tag in sortedTags)
                {
                    bw.Write(Encoding.ASCII.GetBytes(tag));
                    WriteUInt32BE(bw, 0); // Checksum placeholder
                    WriteUInt32BE(bw, 0); // Offset placeholder
                    WriteUInt32BE(bw, 0); // Length placeholder
                }

                Dictionary<string, uint> offsets = new();
                Dictionary<string, uint> lengths = new();
                Dictionary<string, uint> checksums = new();

                foreach (string tag in sortedTags)
                {
                    // Align to 4 bytes
                    while (outStream.Position % 4 != 0)
                    {
                        bw.Write((byte)0);
                    }
                    offsets[tag] = (uint)outStream.Position;
                    byte[] data = outputTables[tag];
                    lengths[tag] = (uint)data.Length;
                    bw.Write(data);
                    checksums[tag] = ComputeTableChecksum(data);
                }

                // Write table directory records
                outStream.Position = tableRecordsPos;
                foreach (string tag in sortedTags)
                {
                    outStream.Position += 4; // Skip tag
                    WriteUInt32BE(bw, checksums[tag]);
                    WriteUInt32BE(bw, offsets[tag]);
                    WriteUInt32BE(bw, lengths[tag]);
                }
            }

            return file;
        }
        finally
        {
            foreach (var cache in tableCache.Values)
                cache.Dispose();
        }
    }




    // ---------------------------
    // Helpers
    // ---------------------------

    private static void Align4(BinaryWriter bw)
    {
        long pad = (4 - (bw.BaseStream.Position & 3)) & 3;
        for (int i = 0; i < pad; i++)
            bw.Write((byte)0);
    }

    private static uint ComputeTableChecksum(byte[] data)
    {
        uint sum = 0;
        int i = 0;
        while (i < data.Length)
        {
            uint value = 0;
            for (int b = 0; b < 4; b++)
            {
                value <<= 8;
                if (i < data.Length)
                    value |= data[i++];
            }
            sum += value;
        }
        return sum;
    }

    // ---------------------------
    // post table build (format 2.0)
    // ---------------------------
    private static byte[] BuildPostTable(IList<FontGlyph> characters, ushort outputNumGlyphs)
    {
        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms, Encoding.UTF8, leaveOpen: true);

        // Format 2.0 (0x00020000)
        WriteUInt32BE(bw, 0x00020000);
        WriteUInt32BE(bw, 0); // italicAngle
        WriteInt16BE(bw, 0);  // underlinePosition
        WriteInt16BE(bw, 0);  // underlineThickness
        WriteUInt32BE(bw, 0); // isFixedPitch
        WriteUInt32BE(bw, 0); // minMemType42
        WriteUInt32BE(bw, 0); // maxMemType42
        WriteUInt32BE(bw, 0); // minMemType1
        WriteUInt32BE(bw, 0); // maxMemType1

        WriteUInt16BE(bw, outputNumGlyphs);

        // GID 0 is .notdef
        WriteUInt16BE(bw, 0);

        // Pass 1: Write index array for all glyphs
        ushort currentStringIndex = 258;
        for (int i = 0; i < characters.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(characters[i].GlyphName))
            {
                WriteUInt16BE(bw, 0);
                continue;
            }

            WriteUInt16BE(bw, currentStringIndex++);
        }

        // Pass 2: Write Pascal strings directly to BinaryWriter
        for (int i = 0; i < characters.Count; i++)
        {
            string name = characters[i].GlyphName;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (name.Length > 255)
                name = name.Substring(0, 255);

            byte[] bytes = Encoding.ASCII.GetBytes(name);
            bw.Write((byte)bytes.Length);
            bw.Write(bytes);
        }

        return ms.ToArray();
    }

    // ---------------------------
    // cmap build (format 4 + 12)
    // ---------------------------

    // --------------------------------------------------------------------------------
    // Builds the standard OpenType 'cmap' table supporting both Format 4 (Unicode BMP)
    // and Format 12 (UCS-4 / Extended Unicode) to map characters to glyph indices.
    // --------------------------------------------------------------------------------
    private static byte[] BuildCmapTable(Dictionary<uint, uint> map)
    {
        Dictionary<uint, uint> bmpMap = map.Where(kv => kv.Key <= 0xFFFF).ToDictionary(kv => kv.Key, kv => kv.Value);
        Dictionary<uint, uint> ucs4Map = map.Where(kv => kv.Key > 0xFFFF).ToDictionary(kv => kv.Key, kv => kv.Value);

        byte[] format4 = bmpMap.Count > 0 ? BuildCmapFormat4(bmpMap) : null;
        byte[] format12 = (bmpMap.Count > 0 || ucs4Map.Count > 0) ? BuildCmapFormat12(map) : null;

        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms, Encoding.UTF8, leaveOpen: true);

        ushort numSubtables = 0;
        if (format4 != null) numSubtables++;
        if (format12 != null) numSubtables++;

        WriteUInt16BE(bw, 0);            // cmap Table Version (always 0)
        WriteUInt16BE(bw, numSubtables); // Number of encoding subtables

        long encodingRecordsPos = ms.Position;

        // Write encoding record placeholders
        if (format4 != null)
        {
            WriteUInt16BE(bw, 3); // platformID: Windows
            WriteUInt16BE(bw, 1); // encodingID: Unicode BMP (UCS-2)
            WriteUInt32BE(bw, 0); // Offset from start of cmap (placeholder)
        }
        if (format12 != null)
        {
            WriteUInt16BE(bw, 3);  // platformID: Windows
            WriteUInt16BE(bw, 10); // encodingID: Unicode Full (UCS-4)
            WriteUInt32BE(bw, 0);  // Offset from start of cmap (placeholder)
        }

        List<uint> offsets = [];

        // Write the subtable payloads and record their actual offsets
        if (format4 != null)
        {
            uint offset = (uint)ms.Position;
            offsets.Add(offset);
            bw.Write(format4);
        }
        if (format12 != null)
        {
            uint offset = (uint)ms.Position;
            offsets.Add(offset);
            bw.Write(format12);
        }

        // Backtrack and fill in the correct offsets in encoding records
        ms.Position = encodingRecordsPos;
        int idx = 0;
        if (format4 != null)
        {
            WriteUInt16BE(bw, 3);
            WriteUInt16BE(bw, 1);
            WriteUInt32BE(bw, offsets[idx++]);
        }
        if (format12 != null)
        {
            WriteUInt16BE(bw, 3);
            WriteUInt16BE(bw, 10);
            WriteUInt32BE(bw, offsets[idx++]);
        }

        return ms.ToArray();
    }

    // --------------------------------------------------------------------------------
    // Builds a Format 4 'cmap' subtable, which is used for character codes <= 0xFFFF.
    // Segmented into contiguous ranges of character codes mapping to glyph indices.
    // --------------------------------------------------------------------------------
    private static byte[] BuildCmapFormat4(Dictionary<uint, uint> map)
    {
        ushort[] codes = map.Keys.Where(k => k <= 0xFFFF).Select(k => (ushort)k).OrderBy(x => x).ToArray();
        if (codes.Length == 0)
            codes = [(ushort)0];

        // Format 4 uses segCount where each segment represents a range of characters.
        // We use segments of size 1 (startCode == endCode) for simplicity and correctness.
        // The last segment must be the sentinel segment mapping 0xFFFF to GID 0.
        int segCount = codes.Length + 1;

        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms, Encoding.UTF8, leaveOpen: true);

        WriteUInt16BE(bw, 4); // Format number: 4

        long lengthPos = ms.Position;
        WriteUInt16BE(bw, 0); // Byte length of this subtable (placeholder)

        WriteUInt16BE(bw, 0); // Language (0 for language-independent)

        WriteUInt16BE(bw, (ushort)(segCount * 2)); // segCountX2

        // Fast search parameters calculation
        int pow2 = 1;
        while (pow2 * 2 <= segCount) pow2 *= 2;
        ushort searchRange = (ushort)(pow2 * 2);
        ushort entrySelector = (ushort)Math.Log(pow2, 2);
        ushort rangeShift = (ushort)(segCount * 2 - searchRange);

        WriteUInt16BE(bw, searchRange);
        WriteUInt16BE(bw, entrySelector);
        WriteUInt16BE(bw, rangeShift);

        // endCode[segCount]
        foreach (ushort c in codes)
            WriteUInt16BE(bw, c);
        WriteUInt16BE(bw, 0xFFFF); // Sentinel endCode

        // reservedPad (always 0)
        WriteUInt16BE(bw, 0);

        // startCode[segCount]
        foreach (ushort c in codes)
            WriteUInt16BE(bw, c);
        WriteUInt16BE(bw, 0xFFFF); // Sentinel startCode

        // idDelta[segCount]
        // idDelta[i] = (glyphId - characterCode) % 65536
        foreach (ushort c in codes)
        {
            uint gid = map[c];
            ushort diff = (ushort)(gid - c);
            short delta = (short)diff; // Modulo arithmetic
            WriteInt16BE(bw, delta);
        }
        WriteInt16BE(bw, 1); // Sentinel segment delta

        // idRangeOffset[segCount] (0 because we map directly using idDelta)
        for (int i = 0; i < segCount; i++)
            WriteUInt16BE(bw, 0);

        long endPos = ms.Position;
        ms.Position = lengthPos;
        WriteUInt16BE(bw, (ushort)endPos);
        ms.Position = endPos;

        return ms.ToArray();
    }

    // --------------------------------------------------------------------------------
    // Builds a Format 12 'cmap' subtable, supporting 32-bit UCS-4 character codes.
    // --------------------------------------------------------------------------------
    private static byte[] BuildCmapFormat12(Dictionary<uint, uint> map)
    {
        uint[] codes = map.Keys.OrderBy(k => k).ToArray();
        if (codes.Length == 0)
            codes = [0u];

        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms, Encoding.UTF8, leaveOpen: true);

        WriteUInt16BE(bw, 12); // Format number: 12
        WriteUInt16BE(bw, 0);  // Reserved (must be 0)

        long lengthPos = ms.Position;
        WriteUInt32BE(bw, 0);  // Byte length of subtable (placeholder)

        WriteUInt32BE(bw, 0);  // Language (0 for language-independent)

        uint nGroups = (uint)codes.Length;
        WriteUInt32BE(bw, nGroups);

        // Groups representation (startCharCode, endCharCode, startGlyphId)
        foreach (uint c in codes)
        {
            uint gid = map[c];
            WriteUInt32BE(bw, c);   // startCharCode
            WriteUInt32BE(bw, c);   // endCharCode
            WriteUInt32BE(bw, gid); // startGlyphId
        }

        long endPos = ms.Position;
        uint length = (uint)endPos;
        ms.Position = lengthPos;
        WriteUInt32BE(bw, length);
        ms.Position = endPos;

        return ms.ToArray();
    }

    private static byte[] RebuildNameTable(
        byte[] originalNameData, 
        string mergedName, 
        string finalCopyright, 
        string finalDescription, 
        string finalDesigner, 
        string finalManufacturer,
        string finalTrademark,
        string finalVendorUrl,
        string finalDesignerUrl,
        string finalLicenseDesc,
        string finalLicenseUrl,
        string finalVersion,
        string finalPreview)
    {
        using MemoryStream msIn = new(originalNameData);
        using BinaryReader br = new(msIn);
        
        ushort format = ReadUInt16BE(br);
        ushort count = ReadUInt16BE(br);
        ushort stringOffset = ReadUInt16BE(br);
        
        List<(ushort PlatformID, ushort EncodingID, ushort LanguageID, ushort NameID, string Value)> records = new();
        
        for (int i = 0; i < count; i++)
        {
            ushort platformID = ReadUInt16BE(br);
            ushort encodingID = ReadUInt16BE(br);
            ushort languageID = ReadUInt16BE(br);
            ushort nameID = ReadUInt16BE(br);
            ushort length = ReadUInt16BE(br);
            ushort offset = ReadUInt16BE(br);
            
            long savePos = msIn.Position;
            msIn.Position = stringOffset + offset;
            byte[] stringBytes = br.ReadBytes(length);
            msIn.Position = savePos;
            
            string val = "";
            if (platformID == 3 || platformID == 0)
            {
                byte[] temp = new byte[stringBytes.Length];
                for (int j = 0; j < temp.Length; j += 2)
                {
                    if (j + 1 < temp.Length)
                    {
                        temp[j] = stringBytes[j+1];
                        temp[j+1] = stringBytes[j];
                    }
                }
                val = Encoding.Unicode.GetString(temp);
            }
            else
            {
                val = Encoding.ASCII.GetString(stringBytes);
            }
            
            records.Add((platformID, encodingID, languageID, nameID, val));
        }

        bool hasCopyright = records.Any(r => r.NameID == 0);
        bool hasDescription = records.Any(r => r.NameID == 10);
        bool hasDesigner = records.Any(r => r.NameID == 9);
        bool hasManufacturer = records.Any(r => r.NameID == 8);
        bool hasTrademark = records.Any(r => r.NameID == 7);
        bool hasVendorUrl = records.Any(r => r.NameID == 11);
        bool hasDesignerUrl = records.Any(r => r.NameID == 12);
        bool hasLicenseDesc = records.Any(r => r.NameID == 13);
        bool hasLicenseUrl = records.Any(r => r.NameID == 14);
        bool hasVersion = records.Any(r => r.NameID == 5);
        bool hasPreview = records.Any(r => r.NameID == 19);
        
        if (!hasCopyright && !string.IsNullOrEmpty(finalCopyright))
            records.Add((3, 1, 1033, 0, finalCopyright));
        if (!hasDescription && !string.IsNullOrEmpty(finalDescription))
            records.Add((3, 1, 1033, 10, finalDescription));
        if (!hasDesigner && !string.IsNullOrEmpty(finalDesigner))
            records.Add((3, 1, 1033, 9, finalDesigner));
        if (!hasManufacturer && !string.IsNullOrEmpty(finalManufacturer))
            records.Add((3, 1, 1033, 8, finalManufacturer));
        if (!hasTrademark && !string.IsNullOrEmpty(finalTrademark))
            records.Add((3, 1, 1033, 7, finalTrademark));
        if (!hasVendorUrl && !string.IsNullOrEmpty(finalVendorUrl))
            records.Add((3, 1, 1033, 11, finalVendorUrl));
        if (!hasDesignerUrl && !string.IsNullOrEmpty(finalDesignerUrl))
            records.Add((3, 1, 1033, 12, finalDesignerUrl));
        if (!hasLicenseDesc && !string.IsNullOrEmpty(finalLicenseDesc))
            records.Add((3, 1, 1033, 13, finalLicenseDesc));
        if (!hasLicenseUrl && !string.IsNullOrEmpty(finalLicenseUrl))
            records.Add((3, 1, 1033, 14, finalLicenseUrl));
        if (!hasVersion && !string.IsNullOrEmpty(finalVersion))
            records.Add((3, 1, 1033, 5, finalVersion));
        if (!hasPreview && !string.IsNullOrEmpty(finalPreview))
            records.Add((3, 1, 1033, 19, finalPreview));
        
        using MemoryStream msOut = new();
        using BinaryWriter bw = new(msOut);
        
        WriteUInt16BE(bw, format);
        WriteUInt16BE(bw, (ushort)records.Count);
        
        long stringOffsetPos = msOut.Position;
        WriteUInt16BE(bw, 0);
        
        using MemoryStream stringMs = new();
        using BinaryWriter stringBw = new(stringMs);
        
        foreach ((ushort PlatformID, ushort EncodingID, ushort LanguageID, ushort NameID, string Value) rec in records)
        {
            string newValue = rec.Value;
            if (rec.NameID == 1 || rec.NameID == 4 || rec.NameID == 16)
                newValue = mergedName;
            else if (rec.NameID == 6)
                newValue = mergedName.Replace(" ", "");
            else if (rec.NameID == 0)
                newValue = finalCopyright;
            else if (rec.NameID == 5)
                newValue = finalVersion;
            else if (rec.NameID == 7)
                newValue = finalTrademark;
            else if (rec.NameID == 8)
                newValue = finalManufacturer;
            else if (rec.NameID == 9)
                newValue = finalDesigner;
            else if (rec.NameID == 10)
                newValue = finalDescription;
            else if (rec.NameID == 11)
                newValue = finalVendorUrl;
            else if (rec.NameID == 12)
                newValue = finalDesignerUrl;
            else if (rec.NameID == 13)
                newValue = finalLicenseDesc;
            else if (rec.NameID == 14)
                newValue = finalLicenseUrl;
            else if (rec.NameID == 19)
                newValue = finalPreview;
            
            byte[] bytes;
            if (rec.PlatformID == 3 || rec.PlatformID == 0)
            {
                byte[] utf16 = Encoding.Unicode.GetBytes(newValue);
                bytes = new byte[utf16.Length];
                for (int j = 0; j < utf16.Length; j += 2)
                {
                    bytes[j] = utf16[j+1];
                    bytes[j+1] = utf16[j];
                }
            }
            else
            {
                bytes = Encoding.ASCII.GetBytes(newValue);
            }
            
            ushort length = (ushort)bytes.Length;
            ushort offset = (ushort)stringMs.Position;
            stringBw.Write(bytes);
            
            WriteUInt16BE(bw, rec.PlatformID);
            WriteUInt16BE(bw, rec.EncodingID);
            WriteUInt16BE(bw, rec.LanguageID);
            WriteUInt16BE(bw, rec.NameID);
            WriteUInt16BE(bw, length);
            WriteUInt16BE(bw, offset);
        }
        
        ushort finalStringOffset = (ushort)msOut.Position;
        bw.Write(stringMs.ToArray());
        
        msOut.Position = stringOffsetPos;
        WriteUInt16BE(bw, finalStringOffset);
        
        return msOut.ToArray();
    }

    // --------------------------------------------------------------------------------
    // Extracts a glyph's outline geometry (supporting both TTF and CFF/PostScript) 
    // using Win2D's geometry path receiver API and encodes it as a standard TrueType
    // simple glyph structure in 'glyf' table format.
    // --------------------------------------------------------------------------------
    private static bool TryGetSimpleGlyphMetrics(byte[] glyphBytes, out ushort pointCount, out ushort contourCount)
    {
        pointCount = 0;
        contourCount = 0;
        if (glyphBytes == null || glyphBytes.Length < 10)
            return false;

        short numContours = (short)((glyphBytes[0] << 8) | glyphBytes[1]);
        if (numContours < 0)
            return false; // Composite glyph

        contourCount = (ushort)numContours;
        if (numContours == 0)
        {
            pointCount = 0;
            return true;
        }

        int lastEndPointOffset = 10 + (numContours - 1) * 2;
        if (lastEndPointOffset + 2 > glyphBytes.Length)
            return false;

        ushort lastEndPoint = (ushort)((glyphBytes[lastEndPointOffset] << 8) | glyphBytes[lastEndPointOffset + 1]);
        pointCount = (ushort)(lastEndPoint + 1);
        return true;
    }

    private static byte[] TryExtractSimpleGlyphDirect(
        CMFontFace fontFace, 
        ushort unitsPerEm, 
        uint gid, 
        float customScale, 
        float offsetX, 
        float offsetY,
        Dictionary<CMFontFace, FontTableCache> tableCache,
        out ushort pointCount,
        out ushort contourCount)
    {
        pointCount = 0;
        contourCount = 0;

        if (customScale != 1f || offsetX != 0f || offsetY != 0f || fontFace.Face.DesignUnitsPerEm != unitsPerEm)
            return null;

        if (!tableCache.TryGetValue(fontFace, out FontTableCache cache))
        {
            cache = new FontTableCache
            {
                Head = fontFace.Face.GetFontTable("head"),
                Loca = fontFace.Face.GetFontTable("loca"),
                GlyfSession = fontFace.Face.OpenTable("glyf")
            };

            if (cache.Head != null && cache.Head.Length >= 52)
                cache.IndexToLocFormat = (short)((cache.Head[50] << 8) | cache.Head[51]);

            tableCache[fontFace] = cache;
        }

        if (cache.Head == null || cache.Loca == null || cache.IndexToLocFormat == -1 || cache.GlyfSession == null || !cache.GlyfSession.Exists)
            return null;

        byte[] loca = cache.Loca;
        short indexToLocFormat = cache.IndexToLocFormat;
        uint startOffset = 0;
        uint endOffset = 0;

        if (indexToLocFormat == 0)
        {
            int startIdx = (int)gid * 2;
            int endIdx = startIdx + 2;
            if (endIdx + 2 > loca.Length)
                return null;

            startOffset = (uint)(((loca[startIdx] << 8) | loca[startIdx + 1]) * 2);
            endOffset = (uint)(((loca[endIdx] << 8) | loca[endIdx + 1]) * 2);
        }
        else if (indexToLocFormat == 1)
        {
            int startIdx = (int)gid * 4;
            int endIdx = startIdx + 4;
            if (endIdx + 4 > loca.Length)
                return null;

            startOffset = (uint)((loca[startIdx] << 24) | (loca[startIdx + 1] << 16) | (loca[startIdx + 2] << 8) | loca[startIdx + 3]);
            endOffset = (uint)((loca[endIdx] << 24) | (loca[endIdx + 1] << 16) | (loca[endIdx + 2] << 8) | loca[endIdx + 3]);
        }
        else
            return null;

        if (startOffset > endOffset)
            return null;

        uint length = endOffset - startOffset;
        if (length == 0)
            return [];

        // Retrieve only the specific glyph outline from memory-mapped glyf table
        byte[] glyphBytes = cache.GlyfSession.GetPart(startOffset, length);
        if (glyphBytes == null)
            return null;

        if (TryGetSimpleGlyphMetrics(glyphBytes, out pointCount, out contourCount))
            return glyphBytes;

        return null;
    }

    private static byte[] ExtractGeometryAsTtf(
        CanvasGeometry geom,
        float customScale, 
        float offsetX, 
        float offsetY, 
        out ushort pointCount, 
        out ushort contourCount)
    {
        try
        {
            TrueTypeGlyphReceiver receiver = new();
            geom.SendPathTo(receiver);

            // Apply custom scaling and offset
            if (customScale != 1f || offsetX != 0f || offsetY != 0f)
            {
                foreach (List<Vector2> contour in receiver.Contours)
                {
                    for (int i = 0; i < contour.Count; i++)
                    {
                        Vector2 pt = contour[i];
                        contour[i] = new Vector2(pt.X * customScale + offsetX, pt.Y * customScale + offsetY);
                    }
                }
            }

            int totalPoints = 0;
            foreach (List<Vector2> c in receiver.Contours) totalPoints += c.Count;
            pointCount = (ushort)totalPoints;
            contourCount = (ushort)receiver.Contours.Count;

            return EncodeSimpleGlyph(receiver.Contours, receiver.PointOnCurve);
        }
        catch (Exception ex)
        {
            pointCount = 0;
            contourCount = 0;
            Utils.AppendDiagnostics("CFF_MERGE_ERROR.txt", $"Custom Geometry: {ex.Message}\r\n{ex.StackTrace}\r\n");
            throw;
        }
    }

    private static byte[] ExtractCffGlyphAsTtf(
        CMFontFace face,
        ushort unitsPerEm, 
        uint gid, 
        float customScale, 
        float offsetX, 
        float offsetY, 
        Dictionary<CMFontFace, FontTableCache> tableCache,
        out ushort pointCount, 
        out ushort contourCount)
    {
        byte[] directBytes = TryExtractSimpleGlyphDirect(face, unitsPerEm, gid, customScale, offsetX, offsetY, tableCache, out pointCount, out contourCount);
        if (directBytes != null)
            return directBytes;

        try
        {
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasGlyph[] glyphs = [
                new() { Index = (int)gid, Advance = 0, AdvanceOffset = 0, AscenderOffset = 0 }
            ];
            CanvasGeometry geom = CanvasGeometry.CreateGlyphRun(
                device,
                new Vector2(0, 0),
                face.FontFace,
                unitsPerEm,
                glyphs,
                false,
                0,
                CanvasTextMeasuringMode.Natural,
                CanvasGlyphOrientation.Upright);
            TrueTypeGlyphReceiver receiver = new();
            geom.SendPathTo(receiver);

            // Apply custom scaling and offset
            if (customScale != 1f || offsetX != 0f || offsetY != 0f)
            {
                foreach (List<Vector2> contour in receiver.Contours)
                {
                    for (int i = 0; i < contour.Count; i++)
                    {
                        Vector2 pt = contour[i];
                        contour[i] = new Vector2(pt.X * customScale + offsetX, pt.Y * customScale + offsetY);
                    }
                }
            }

            int totalPoints = 0;
            foreach (List<Vector2> c in receiver.Contours) totalPoints += c.Count;
            pointCount = (ushort)totalPoints;
            contourCount = (ushort)receiver.Contours.Count;

            return EncodeSimpleGlyph(receiver.Contours, receiver.PointOnCurve);
        }
        catch (Exception ex)
        {
            pointCount = 0;
            contourCount = 0;
            Utils.AppendDiagnostics("CFF_MERGE_ERROR.txt", $"GID {gid}: {ex.Message}\r\n{ex.StackTrace}\r\n");
        
            throw ex;
        }
    }

    public static byte[] EncodeSimpleGlyph(List<List<Vector2>> contours, List<bool> onCurveFlags)
    {
        if (contours.Count == 0)
            return Array.Empty<byte>();

        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms);

        short numContours = (short)contours.Count;
        WriteInt16BE(bw, numContours);

        short xMin = short.MaxValue;
        short yMin = short.MaxValue;
        short xMax = short.MinValue;
        short yMax = short.MinValue;

        List<short> allX = [];
        List<short> allY = [];
        List<ushort> endPtsOfContours = [];

        ushort pointIndex = 0;
        for (int c = 0; c < contours.Count; c++)
        {
            List<Vector2> contour = contours[c];
            for (int p = 0; p < contour.Count; p++)
            {
                Vector2 pt = contour[p];
                short x = (short)Math.Round(pt.X);
                short y = (short)Math.Round(-pt.Y); // Flip Y

                xMin = Math.Min(xMin, x);
                yMin = Math.Min(yMin, y);
                xMax = Math.Max(xMax, x);
                yMax = Math.Max(yMax, y);

                allX.Add(x);
                allY.Add(y);
            }
            pointIndex += (ushort)contour.Count;
            endPtsOfContours.Add((ushort)(pointIndex - 1));
        }

        WriteInt16BE(bw, xMin);
        WriteInt16BE(bw, yMin);
        WriteInt16BE(bw, xMax);
        WriteInt16BE(bw, yMax);

        // endPtsOfContours[]
        for (int i = 0; i < numContours; i++)
            WriteUInt16BE(bw, endPtsOfContours[i]);

        // instructionLength
        WriteUInt16BE(bw, 0);

        // Prepare streams for coordinates and flags
        byte[] flags = new byte[allX.Count];
        using MemoryStream xMs = new();
        using BinaryWriter xBw = new(xMs);
        using MemoryStream yMs = new();
        using BinaryWriter yBw = new(yMs);

        short prevX = 0;
        short prevY = 0;

        for (int i = 0; i < allX.Count; i++)
        {
            byte flag = (byte)(onCurveFlags[i] ? 0x01 : 0x00);
            short dx = (short)(allX[i] - prevX);
            short dy = (short)(allY[i] - prevY);

            // Compress X Coordinate
            if (dx == 0)
                flag |= 0x10;
            else if (dx >= -255 && dx <= 255)
            {
                flag |= 0x02;
                if (dx > 0)
                    flag |= 0x10;
                xBw.Write((byte)Math.Abs(dx));
            }
            else
                WriteInt16BE(xBw, dx);

            // Compress Y Coordinate
            if (dy == 0)
                flag |= 0x20;
            else if (dy >= -255 && dy <= 255)
            {
                flag |= 0x04;
                if (dy > 0)
                    flag |= 0x20;
                yBw.Write((byte)Math.Abs(dy));
            }
            else
                WriteInt16BE(yBw, dy);

            flags[i] = flag;
            prevX = allX[i];
            prevY = allY[i];
        }

        // Write flags
        bw.Write(flags);

        // Write coordinate byte streams
        bw.Write(xMs.ToArray());
        bw.Write(yMs.ToArray());

        return ms.ToArray();
    }

    private static ushort GetMostRestrictiveFsType(ushort fsType1, ushort fsType2)
    {
        if ((fsType1 & 0x0002) != 0 || (fsType2 & 0x0002) != 0)
            return 0x0002;
        if ((fsType1 & 0x0004) != 0 || (fsType2 & 0x0004) != 0)
            return 0x0004;
        if ((fsType1 & 0x0008) != 0 || (fsType2 & 0x0008) != 0)
            return 0x0008;
        return 0;
    }
}

public class TrueTypeGlyphReceiver : ICanvasPathReceiver
{
    public List<List<Vector2>> Contours { get; } = new();
    public List<bool> PointOnCurve { get; } = new();

    private List<Vector2> _currentContour = null;

    public void BeginFigure(Vector2 startPoint, CanvasFigureFill fill)
    {
        _currentContour = new List<Vector2>();
        _currentContour.Add(startPoint);
        PointOnCurve.Add(true);
    }

    public void AddLine(Vector2 endPoint)
    {
        if (_currentContour != null)
        {
            _currentContour.Add(endPoint);
            PointOnCurve.Add(true);
        }
    }

    public void AddQuadraticBezier(Vector2 controlPoint, Vector2 endPoint)
    {
        if (_currentContour != null)
        {
            _currentContour.Add(controlPoint);
            PointOnCurve.Add(false);
            _currentContour.Add(endPoint);
            PointOnCurve.Add(true);
        }
    }

    public void AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint)
    {
        if (_currentContour != null && _currentContour.Count > 0)
        {
            Vector2 p0 = _currentContour[^1];
            Vector2 p1 = controlPoint1;
            Vector2 p2 = controlPoint2;
            Vector2 p3 = endPoint;

            Vector2 q1 = p0 + 0.75f * (p1 - p0);
            Vector2 q2 = p3 + 0.75f * (p2 - p3);
            Vector2 m = (q1 + q2) / 2.0f;

            _currentContour.Add(q1);
            PointOnCurve.Add(false);
            _currentContour.Add(m);
            PointOnCurve.Add(true);

            _currentContour.Add(q2);
            PointOnCurve.Add(false);
            _currentContour.Add(p3);
            PointOnCurve.Add(true);
        }
    }

    public void EndFigure(CanvasFigureLoop figureLoop)
    {
        if (_currentContour != null && _currentContour.Count > 0)
        {
            Contours.Add(_currentContour);
        }
        _currentContour = null;
    }

    public void AddArc(Vector2 endPoint, float radiusX, float radiusY, float rotationAngle, CanvasSweepDirection sweepDirection, CanvasArcSize arcSize) { }
    public void SetFilledRegionDetermination(CanvasFilledRegionDetermination filledRegionDetermination) { }
    public void SetSegmentOptions(CanvasFigureSegmentOptions figureSegmentOptions) { }
}
