using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Xml.Linq;

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

    // ---------------------------
    // Big-endian helpers
    // ---------------------------

    static ushort ReadUInt16BE(BinaryReader br) => SfntWriter.ReadUInt16BE(br);
    static short ReadInt16BE(BinaryReader br) => SfntWriter.ReadInt16BE(br);
    static uint ReadUInt32BE(BinaryReader br) => SfntWriter.ReadUInt32BE(br);
    static void WriteUInt16BE(BinaryWriter bw, ushort v) => SfntWriter.WriteUInt16BE(bw, v);
    static void WriteUInt32BE(BinaryWriter bw, uint v) => SfntWriter.WriteUInt32BE(bw, v);
    static void WriteInt16BE(BinaryWriter bw, short v) => SfntWriter.WriteInt16BE(bw, v);

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

    public static async Task<StorageFile> CreateSubsetAsync(SubsetOptions opts)
    {
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

        byte[] newCmapTable = SfntWriter.BuildCmapTable(outputCmap);

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

            CMSVTable cmTable = CMSVTable.TryDecode(templateFont) ?? new();

            // 6.2. Merge OS/2 embedding rights and ranges
            OS2Metadata os2Meta = SfntWriter.CalculateMergedOS2(uniqueFonts, fontFsTypes, templateFont);

            // 6.3. Rebuild tables to match output
            byte[] newNameTable = SfntWriter.RebuildNameTable(opts, templateFont, uniqueFonts, cmTable);
            byte[] headData = SfntWriter.RebuildHeadTable(opts, templateFont);
            byte[] os2Data = SfntWriter.BuildOS2Table(templateFont, os2Meta);
            byte[] postData = SfntWriter.BuildPostTable(characters, outputNumGlyphs);
            byte[] newLocaData = SfntWriter.BuildLocaTable(outputNumGlyphs, newLoca);

            // 6.4. Update hhea table with new glyph count
            byte[] hheaData = templateFont.Face.GetFontTable("hhea");
            if (hheaData == null) throw new InvalidDataException("Missing hhea table in template font");
            hheaData[34] = (byte)(outputNumGlyphs >> 8);
            hheaData[35] = (byte)(outputNumGlyphs & 0xFF);

            // 6.5. Rebuild maxp tables
            byte[] maxpData = new byte[32];
            maxpData[1] = 1; // version 1.0 (0x00010000)
            maxpData[4] = (byte)(outputNumGlyphs >> 8);
            maxpData[5] = (byte)(outputNumGlyphs & 0xFF);
            maxpData[6] = (byte)(maxPointsOfAll >> 8);
            maxpData[7] = (byte)(maxPointsOfAll & 0xFF);
            maxpData[8] = (byte)(maxContoursOfAll >> 8);
            maxpData[9] = (byte)(maxContoursOfAll & 0xFF);
            maxpData[15] = 1; // maxZones = 1

            // 6.6. Assign all tables to output dictionary
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
            outputTables[CMSVTable.TAG] = cmTable.Encode();


            // 7. Write the final font file to disk
            StorageFile file = opts.OutputFile
                ?? await StorageHelper.CreateTempFileAsync($"SS\\{opts.DesiredName}.ttf").AsTask().ConfigureAwait(false);

            using (Stream outStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
            {
                SfntWriter.WriteSfntFile(outStream, outputTables);
            }

            return file;
        }
        finally
        {
            foreach (var cache in tableCache.Values)
                cache.Dispose();
        }
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
