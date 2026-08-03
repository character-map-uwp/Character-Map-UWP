using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Helpers;

public class OS2Metadata
{
    public ushort FsType { get; set; }
    public uint UnicodeRange1 { get; set; }
    public uint UnicodeRange2 { get; set; }
    public uint UnicodeRange3 { get; set; }
    public uint UnicodeRange4 { get; set; }
    public uint CodePageRange1 { get; set; }
    public uint CodePageRange2 { get; set; }
}

public record CMSVFontInfo(string Name, string Version);

public class CMSVTable
{
    /*                        CMSV Table Spec
    * +-------------------------------------------------------------+
    * | Header (26 bytes for v1.1, 18 bytes for v1.0)               |
    * | - majorVersion, minorVersion                                |
    * | - appVersion (Major, Minor, Build, Revision)                |
    * | - appVersionStringOffset & Length                           |
    * | - classNameOffset & Length (v1.1+)                          |
    * | - namespaceOffset & Length (v1.1+)                          |
    * | - fontFaceCount                                             |
    * +-------------------------------------------------------------+
    * | Font Face Records Array [fontFaceCount * 8 bytes]           |
    * | - Record 0: nameOffset, nameLength, versionOffset, etc.     |
    * | - Record 1: ...                                             |
    * +-------------------------------------------------------------+
    * | String Data Block (Variable Length)                         |
    * | - App Version string bytes                                  |
    * | - Class Name string bytes (v1.1+)                           |
    * | - Namespace string bytes (v1.1+)                            |
    * | - Font Face Name & Version string bytes                     |
    * +-------------------------------------------------------------+
    */

    public const string TAG = "CMSV";
    public const ushort MAJOR_VERSION = 1;
    public const ushort MINOR_VERSION = 1;

    public ushort AppMajor { get; set; } = 5;
    public ushort AppMinor { get; set; } = 0;
    public ushort AppBuild { get; set; } = 0;
    public ushort AppRevision { get; set; } = 0;
    public string AppVersionString { get; set; }
    public string ClassName { get; set; }
    public string Namespace { get; set; }
    public List<CMSVFontInfo> FontFaces { get; set; } = [];

    public bool IsExisting => !string.IsNullOrEmpty(AppVersionString);

    public CMSVTable UpdateVersion()
    {
        try
        {
            Windows.ApplicationModel.PackageVersion ver = Windows.ApplicationModel.Package.Current.Id.Version;
            this.AppMajor = ver.Major;
            this.AppMinor = ver.Minor;
            this.AppBuild = ver.Build;
            this.AppRevision = ver.Revision;
            this.AppVersionString = $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
        }
        catch
        {
            this.AppVersionString = "Unknown";
        }

        return this;
    }

    public byte[] Encode(bool updateVersion = true)
    {
        if (updateVersion)
            UpdateVersion();

        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms);

        byte[] appVerBytes = Encoding.UTF8.GetBytes(AppVersionString ?? string.Empty);
        byte[] classNameBytes = Encoding.UTF8.GetBytes(ClassName ?? string.Empty);
        byte[] namespaceBytes = Encoding.UTF8.GetBytes(Namespace ?? string.Empty);
        ushort count = (ushort)FontFaces.Count;

        // Calculate string block start offset:
        // Header size (26 bytes) + FontFaceRecords (count * 8 bytes)
        ushort currentOffset = (ushort)(26 + (count * 8));

        // Write Header
        WriteUInt16BE(bw, MAJOR_VERSION);
        WriteUInt16BE(bw, MINOR_VERSION);
        WriteUInt16BE(bw, AppMajor);
        WriteUInt16BE(bw, AppMinor);
        WriteUInt16BE(bw, AppBuild);
        WriteUInt16BE(bw, AppRevision);

        // App version string offset & length
        WriteUInt16BE(bw, currentOffset);
        WriteUInt16BE(bw, (ushort)appVerBytes.Length);
        currentOffset += (ushort)appVerBytes.Length;

        // Class name string offset & length (v1.1)
        WriteUInt16BE(bw, currentOffset);
        WriteUInt16BE(bw, (ushort)classNameBytes.Length);
        currentOffset += (ushort)classNameBytes.Length;

        // Namespace string offset & length (v1.1)
        WriteUInt16BE(bw, currentOffset);
        WriteUInt16BE(bw, (ushort)namespaceBytes.Length);
        currentOffset += (ushort)namespaceBytes.Length;

        // Font face count
        WriteUInt16BE(bw, count);

        // Prepare string buffers
        List<byte[]> nameBytesList = [];
        List<byte[]> versionBytesList = [];

        foreach (CMSVFontInfo font in FontFaces)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(font.Name ?? string.Empty);
            byte[] versionBytes = Encoding.UTF8.GetBytes(font.Version ?? string.Empty);

            nameBytesList.Add(nameBytes);
            versionBytesList.Add(versionBytes);

            // Write Record
            WriteUInt16BE(bw, currentOffset);
            WriteUInt16BE(bw, (ushort)nameBytes.Length);
            currentOffset += (ushort)nameBytes.Length;

            WriteUInt16BE(bw, currentOffset);
            WriteUInt16BE(bw, (ushort)versionBytes.Length);
            currentOffset += (ushort)versionBytes.Length;
        }

        // Write String Data Block
        bw.Write(appVerBytes);
        bw.Write(classNameBytes);
        bw.Write(namespaceBytes);
        for (int i = 0; i < count; i++)
        {
            bw.Write(nameBytesList[i]);
            bw.Write(versionBytesList[i]);
        }

        return ms.ToArray();
    }

    public static CMSVTable TryDecode(CMFontFace fontFace)
    {
        byte[] existingData = fontFace.Face.GetFontTable(CMSVTable.TAG);
        return CMSVTable.TryDecode(existingData);
    }

    public static CMSVTable TryDecode(byte[] data)
    {
        if (data == null || data.Length < 18) return null;

        try
        {
            using MemoryStream ms = new(data);
            using BinaryReader br = new(ms);

            ushort major = ReadUInt16BE(br);
            ushort minor = ReadUInt16BE(br);
            if (major != MAJOR_VERSION) return null;

            CMSVTable table = new()
            {
                AppMajor = ReadUInt16BE(br),
                AppMinor = ReadUInt16BE(br),
                AppBuild = ReadUInt16BE(br),
                AppRevision = ReadUInt16BE(br)
            };

            ushort appVerOffset = ReadUInt16BE(br);
            ushort appVerLength = ReadUInt16BE(br);

            ushort classNameOffset = 0;
            ushort classNameLength = 0;
            ushort namespaceOffset = 0;
            ushort namespaceLength = 0;
            ushort fontCount = 0;

            if (minor >= 1)
            {
                if (data.Length >= 26)
                {
                    classNameOffset = ReadUInt16BE(br);
                    classNameLength = ReadUInt16BE(br);
                    namespaceOffset = ReadUInt16BE(br);
                    namespaceLength = ReadUInt16BE(br);
                    fontCount = ReadUInt16BE(br);
                }
                else if (data.Length >= 22)
                {
                    classNameOffset = ReadUInt16BE(br);
                    classNameLength = ReadUInt16BE(br);
                    fontCount = ReadUInt16BE(br);
                }
                else
                {
                    fontCount = ReadUInt16BE(br);
                }
            }
            else
            {
                fontCount = ReadUInt16BE(br);
            }

            if (appVerOffset + appVerLength <= data.Length && appVerLength > 0)
                table.AppVersionString = Encoding.UTF8.GetString(data, appVerOffset, appVerLength);

            if (classNameOffset > 0 && classNameOffset + classNameLength <= data.Length && classNameLength > 0)
                table.ClassName = Encoding.UTF8.GetString(data, classNameOffset, classNameLength);

            if (namespaceOffset > 0 && namespaceOffset + namespaceLength <= data.Length && namespaceLength > 0)
                table.Namespace = Encoding.UTF8.GetString(data, namespaceOffset, namespaceLength);

            for (int i = 0; i < fontCount; i++)
            {
                ushort nameOffset = ReadUInt16BE(br);
                ushort nameLength = ReadUInt16BE(br);
                ushort verOffset = ReadUInt16BE(br);
                ushort verLength = ReadUInt16BE(br);

                string name = (nameOffset + nameLength <= data.Length)
                    ? Encoding.UTF8.GetString(data, nameOffset, nameLength)
                    : string.Empty;

                string ver = (verOffset + verLength <= data.Length)
                    ? Encoding.UTF8.GetString(data, verOffset, verLength)
                    : string.Empty;

                table.FontFaces.Add(new(name, ver));
            }

            return table;
        }
        catch (Exception ex)
        {
            Utils.AppendDiagnostics("CMSV TABLE DECODE", ex);
        }

        return null;
    }

    static ushort ReadUInt16BE(BinaryReader br) => (ushort)((br.ReadByte() << 8) | br.ReadByte());
    static void WriteUInt16BE(BinaryWriter bw, ushort v)
    {
        bw.Write((byte)(v >> 8));
        bw.Write((byte)(v & 0xFF));
    }
}




/// <summary>
/// Provides low-level OpenType/SFNT binary writing, checksum calculation,
/// table directory assembly, and standard table building functionality (cmap, post, name).
/// </summary>
public static class SfntWriter
{
    #region Big-Endian Binary Helpers

    public static ushort ReadUInt16BE(BinaryReader br)
    {
        byte[] b = br.ReadBytes(2);
        return (ushort)((b[0] << 8) | b[1]);
    }

    public static short ReadInt16BE(BinaryReader br)
    {
        byte[] b = br.ReadBytes(2);
        return (short)((b[0] << 8) | b[1]);
    }

    public static uint ReadUInt32BE(BinaryReader br)
    {
        byte[] b = br.ReadBytes(4);
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    public static void WriteUInt16BE(BinaryWriter bw, ushort v)
    {
        bw.Write((byte)(v >> 8));
        bw.Write((byte)(v & 0xFF));
    }

    public static void WriteUInt32BE(BinaryWriter bw, uint v)
    {
        bw.Write((byte)(v >> 24));
        bw.Write((byte)((v >> 16) & 0xFF));
        bw.Write((byte)((v >> 8) & 0xFF));
        bw.Write((byte)(v & 0xFF));
    }

    public static void WriteInt16BE(BinaryWriter bw, short v)
    {
        bw.Write((byte)((v >> 8) & 0xFF));
        bw.Write((byte)(v & 0xFF));
    }

    public static void Align4(BinaryWriter bw)
    {
        long pad = (4 - (bw.BaseStream.Position & 3)) & 3;
        for (int i = 0; i < pad; i++)
            bw.Write((byte)0);
    }

    public static uint ComputeTableChecksum(byte[] data)
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

    #endregion

    #region SFNT File Compilation

    /// <summary>
    /// Writes a dictionary of raw OpenType tables into a complete SFNT (.ttf) file container stream.
    /// </summary>
    public static void WriteSfntFile(Stream outStream, IDictionary<string, byte[]> outputTables)
    {
        ushort numTables = (ushort)outputTables.Count;
        int maxPower2 = 1;
        while (maxPower2 * 2 <= numTables) maxPower2 *= 2;
        ushort searchRange = (ushort)(maxPower2 * 16);
        ushort entrySelector = (ushort)Math.Log(maxPower2, 2);
        ushort rangeShift = (ushort)(numTables * 16 - searchRange);

        using BinaryWriter bw = new(outStream, Encoding.UTF8, leaveOpen: true);
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
                bw.Write((byte)0);

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


    #endregion

    #region Standard Table Builders

    public static byte[] BuildLocaTable(ushort outputNumGlyphs, uint[] newLoca)
    {
        byte[] newLocaData;
        using (MemoryStream locaMs = new())
        using (BinaryWriter locaBw = new(locaMs))
        {
            for (int i = 0; i <= outputNumGlyphs; i++)
                WriteUInt32BE(locaBw, newLoca[i]);
            newLocaData = locaMs.ToArray();
        }

        return newLocaData;
    }

    public static byte[] RebuildHeadTable(SubsetOptions opts, CMFontFace templateFont)
    {
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

        return headData;
    }

    public static byte[] RebuildNameTable(SubsetOptions opts, CMFontFace templateFont, List<CMFontFace> uniqueFonts, CMSVTable cmTable)
    {
        List<CMFontFace> metadataFonts = uniqueFonts
              .GroupBy(f => f.FullName)
              .Select(g => g.First())
              .ToList();

        HashSet<string> existingFontKeys = [];
        foreach (CMFontFace font in metadataFonts)
        {
            if (cmTable.FontFaces.Any(f => f.Name == font.FullName && f.Version == font.Version))
                existingFontKeys.Add(font.Key);
            else
                cmTable.FontFaces.Add(new(font.FullName, font.Version));
        }

        string MergeField(CanvasFontInformation info)
        {
            List<string> distinctValues = metadataFonts
                .Select(f => f.TryGetInfo(info)?.Value ?? string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();

            if (distinctValues.Count == 1)
                return distinctValues[0];

            List<string> lines = [];
            foreach (CMFontFace font in metadataFonts)
            {
                string fontName = font.FullName;
                string key = font.Key;

                // If fontface name & version already exists inside existing cmTable data (already subsetted),
                // don't apply it again
                if (existingFontKeys.Contains(key))
                    continue; ;

                string valStr = font.TryGetInfo(info)?.Value;
                if (!string.IsNullOrWhiteSpace(valStr))
                    lines.Add($"{fontName}:\n{valStr}\n");
                //else
                //    lines.Add($"{fontName}:\nNot specified\n");
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

        List<string> fontNames = metadataFonts.Select(f => $"{f.FullName} ({f.Version})".Trim()).ToList();
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
                foreach (FontGlyph c in opts.Characters)
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
                newNameTable = SfntWriter.RebuildNameTable(
                    nameData,
                    opts.DesiredName,
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

        return newNameTable;
    }


    /// <summary>
    /// Builds a Format 2.0 'post' table for named glyphs.
    /// </summary>
    public static byte[] BuildPostTable(IList<FontGlyph> characters, ushort outputNumGlyphs)
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

    /// <summary>
    /// Builds the standard OpenType 'cmap' table supporting Format 4 (Unicode BMP)
    /// and Format 12 (UCS-4 / Extended Unicode) to map characters to glyph indices.
    /// </summary>
    public static byte[] BuildCmapTable(Dictionary<uint, uint> map)
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

    public static OS2Metadata CalculateMergedOS2(List<CMFontFace> uniqueFonts, Dictionary<CMFontFace, ushort> fontFsTypes, CMFontFace templateFont)
    {
        static ushort GetMostRestrictiveFsType(ushort fsType1, ushort fsType2)
        {
            if ((fsType1 & 0x0002) != 0 || (fsType2 & 0x0002) != 0)
                return 0x0002;
            if ((fsType1 & 0x0004) != 0 || (fsType2 & 0x0004) != 0)
                return 0x0004;
            if ((fsType1 & 0x0008) != 0 || (fsType2 & 0x0008) != 0)
                return 0x0008;
            return 0;
        }

        OS2Metadata os2Meta = new();

        if (uniqueFonts.Count > 0)
        {
            foreach (CMFontFace font in uniqueFonts)
            {
                os2Meta.FsType = GetMostRestrictiveFsType(os2Meta.FsType, fontFsTypes[font]);

                byte[] fontOs2 = font.Face.GetFontTable("OS/2");
                if (fontOs2 != null)
                {
                    // Read ulUnicodeRange1-4 (128-bit bitfield at offset 42, 16 bytes total) from the OS/2 table.
                    // Each 32-bit big-endian uint represents supported Unicode character ranges (blocks).
                    // Bitwise-OR ('|=') merges range support across all source fonts so the output font retains them.
                    if (fontOs2.Length >= 58)
                    {
                        uint fr1 = (uint)((fontOs2[42] << 24) | (fontOs2[43] << 16) | (fontOs2[44] << 8) | fontOs2[45]);
                        uint fr2 = (uint)((fontOs2[46] << 24) | (fontOs2[47] << 16) | (fontOs2[48] << 8) | fontOs2[49]);
                        uint fr3 = (uint)((fontOs2[50] << 24) | (fontOs2[51] << 16) | (fontOs2[52] << 8) | fontOs2[53]);
                        uint fr4 = (uint)((fontOs2[54] << 24) | (fontOs2[55] << 16) | (fontOs2[56] << 8) | fontOs2[57]);
                        os2Meta.UnicodeRange1 |= fr1;
                        os2Meta.UnicodeRange2 |= fr2;
                        os2Meta.UnicodeRange3 |= fr3;
                        os2Meta.UnicodeRange4 |= fr4;
                    }

                    // Read ulCodePageRange1-2 (64-bit bitfield at offset 78, 8 bytes total) from OS/2 table v1+.
                    // Each 32-bit big-endian uint represents supported Windows/OEM code pages.
                    // Bitwise-OR ('|=') combines all supported code page ranges across all source fonts.
                    if (fontOs2.Length >= 86)
                    {
                        uint fc1 = (uint)((fontOs2[78] << 24) | (fontOs2[79] << 16) | (fontOs2[80] << 8) | fontOs2[81]);
                        uint fc2 = (uint)((fontOs2[82] << 24) | (fontOs2[83] << 16) | (fontOs2[84] << 8) | fontOs2[85]);
                        os2Meta.CodePageRange1 |= fc1;
                        os2Meta.CodePageRange2 |= fc2;
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
                    os2Meta.UnicodeRange1 = (uint)((fontOs2[42] << 24) | (fontOs2[43] << 16) | (fontOs2[44] << 8) | fontOs2[45]);
                    os2Meta.UnicodeRange2 = (uint)((fontOs2[46] << 24) | (fontOs2[47] << 16) | (fontOs2[48] << 8) | fontOs2[49]);
                    os2Meta.UnicodeRange3 = (uint)((fontOs2[50] << 24) | (fontOs2[51] << 16) | (fontOs2[52] << 8) | fontOs2[53]);
                    os2Meta.UnicodeRange4 = (uint)((fontOs2[54] << 24) | (fontOs2[55] << 16) | (fontOs2[56] << 8) | fontOs2[57]);
                }
                if (fontOs2.Length >= 86)
                {
                    os2Meta.CodePageRange1 = (uint)((fontOs2[78] << 24) | (fontOs2[79] << 16) | (fontOs2[80] << 8) | fontOs2[81]);
                    os2Meta.CodePageRange2 = (uint)((fontOs2[82] << 24) | (fontOs2[83] << 16) | (fontOs2[84] << 8) | fontOs2[85]);
                }
            }
        }

        return os2Meta;
    }


    public static byte[] BuildOS2Table(CMFontFace templateFont, OS2Metadata os2Meta)
    {
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
            os2Data[8] = (byte)(os2Meta.FsType >> 8);
            os2Data[9] = (byte)(os2Meta.FsType & 0xFF);
        }

        // Manually update ulUnicodeRange1-4 (supported Unicode ranges) at offset 42 (16 bytes)
        if (os2Data.Length >= 58)
        {
            os2Data[42] = (byte)(os2Meta.UnicodeRange1 >> 24);
            os2Data[43] = (byte)((os2Meta.UnicodeRange1 >> 16) & 0xFF);
            os2Data[44] = (byte)((os2Meta.UnicodeRange1 >> 8) & 0xFF);
            os2Data[45] = (byte)(os2Meta.UnicodeRange1 & 0xFF);

            os2Data[46] = (byte)(os2Meta.UnicodeRange2 >> 24);
            os2Data[47] = (byte)((os2Meta.UnicodeRange2 >> 16) & 0xFF);
            os2Data[48] = (byte)((os2Meta.UnicodeRange2 >> 8) & 0xFF);
            os2Data[49] = (byte)(os2Meta.UnicodeRange2 & 0xFF);

            os2Data[50] = (byte)(os2Meta.UnicodeRange3 >> 24);
            os2Data[51] = (byte)((os2Meta.UnicodeRange3 >> 16) & 0xFF);
            os2Data[52] = (byte)((os2Meta.UnicodeRange3 >> 8) & 0xFF);
            os2Data[53] = (byte)(os2Meta.UnicodeRange3 & 0xFF);

            os2Data[54] = (byte)(os2Meta.UnicodeRange4 >> 24);
            os2Data[55] = (byte)((os2Meta.UnicodeRange4 >> 16) & 0xFF);
            os2Data[56] = (byte)((os2Meta.UnicodeRange4 >> 8) & 0xFF);
            os2Data[57] = (byte)(os2Meta.UnicodeRange4 & 0xFF);
        }

        // Manually update ulCodePageRange1-2 (supported codepages) at offset 78 (8 bytes)
        if (os2Data.Length >= 86)
        {
            os2Data[78] = (byte)(os2Meta.CodePageRange1 >> 24);
            os2Data[79] = (byte)((os2Meta.CodePageRange1 >> 16) & 0xFF);
            os2Data[80] = (byte)((os2Meta.CodePageRange1 >> 8) & 0xFF);
            os2Data[81] = (byte)(os2Meta.CodePageRange1 & 0xFF);

            os2Data[82] = (byte)(os2Meta.CodePageRange2 >> 24);
            os2Data[83] = (byte)((os2Meta.CodePageRange2 >> 16) & 0xFF);
            os2Data[84] = (byte)((os2Meta.CodePageRange2 >> 8) & 0xFF);
            os2Data[85] = (byte)(os2Meta.CodePageRange2 & 0xFF);
        }

        return os2Data;
    }


    /// <summary>
    /// Builds a Format 4 'cmap' subtable for character codes <= 0xFFFF.
    /// </summary>
    public static byte[] BuildCmapFormat4(Dictionary<uint, uint> map)
    {
        ushort[] codes = map.Keys.Where(k => k <= 0xFFFF).Select(k => (ushort)k).OrderBy(x => x).ToArray();
        if (codes.Length == 0)
            codes = [(ushort)0];

        int segCount = codes.Length + 1;

        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms, Encoding.UTF8, leaveOpen: true);

        WriteUInt16BE(bw, 4); // Format number: 4

        long lengthPos = ms.Position;
        WriteUInt16BE(bw, 0); // Byte length placeholder

        WriteUInt16BE(bw, 0); // Language

        WriteUInt16BE(bw, (ushort)(segCount * 2)); // segCountX2

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

        // reservedPad
        WriteUInt16BE(bw, 0);

        // startCode[segCount]
        foreach (ushort c in codes)
            WriteUInt16BE(bw, c);
        WriteUInt16BE(bw, 0xFFFF); // Sentinel startCode

        // idDelta[segCount]
        foreach (ushort c in codes)
        {
            uint gid = map[c];
            ushort diff = (ushort)(gid - c);
            short delta = (short)diff;
            WriteInt16BE(bw, delta);
        }
        WriteInt16BE(bw, 1); // Sentinel segment delta

        // idRangeOffset[segCount]
        for (int i = 0; i < segCount; i++)
            WriteUInt16BE(bw, 0);

        long endPos = ms.Position;
        ms.Position = lengthPos;
        WriteUInt16BE(bw, (ushort)endPos);
        ms.Position = endPos;

        return ms.ToArray();
    }

    /// <summary>
    /// Builds a Format 12 'cmap' subtable supporting 32-bit UCS-4 character codes.
    /// </summary>
    public static byte[] BuildCmapFormat12(Dictionary<uint, uint> map)
    {
        uint[] codes = map.Keys.OrderBy(k => k).ToArray();
        if (codes.Length == 0)
            codes = [0u];

        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms, Encoding.UTF8, leaveOpen: true);

        WriteUInt16BE(bw, 12); // Format number: 12
        WriteUInt16BE(bw, 0);  // Reserved

        long lengthPos = ms.Position;
        WriteUInt32BE(bw, 0);  // Byte length placeholder

        WriteUInt32BE(bw, 0);  // Language

        uint nGroups = (uint)codes.Length;
        WriteUInt32BE(bw, nGroups);

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

    /// <summary>
    /// Rebuilds the OpenType 'name' table with updated metadata strings.
    /// </summary>
    public static byte[] RebuildNameTable(
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

        List<(ushort PlatformID, ushort EncodingID, ushort LanguageID, ushort NameID, string Value)> records = [];

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
                        temp[j] = stringBytes[j + 1];
                        temp[j + 1] = stringBytes[j];
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
                    bytes[j] = utf16[j + 1];
                    bytes[j + 1] = utf16[j];
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

    #endregion
}
