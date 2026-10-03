using CharacterMap.Core;
using CharacterMapCX;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CharacterMap.Helpers;

internal class SVGGlyphHelper
{




    //------------------------------------------------------
    //
    //  SVG -> TTF Glyph Creation
    //
    //------------------------------------------------------

    #region Glyph Creation

    public static async Task<FontGlyph> TryLoadFontGlyphAsync(StorageFile file, uint nextPUA)
    {
        try
        {
            string svgText = await FileIO.ReadTextAsync(file);
            List<string> pathDatas = ExtractPaths(svgText);

            if (pathDatas.Count == 0)
                return null;

            float viewBoxX = 0f;
            float viewBoxY = 0f;
            float viewBoxWidth = 0f;
            float viewBoxHeight = 0f;

            int svgIndex = svgText.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (svgIndex != -1)
            {
                int svgEnd = svgText.IndexOf('>', svgIndex);
                if (svgEnd != -1)
                {
                    string svgTag = svgText.Substring(svgIndex, svgEnd - svgIndex);

                    int viewBoxIdx = FindAttributeIndex(svgTag, "viewBox");
                    if (viewBoxIdx != -1)
                    {
                        string viewBoxAttr = ExtractAttributeValue(svgTag, viewBoxIdx);
                        if (!string.IsNullOrWhiteSpace(viewBoxAttr))
                        {
                            string[] parts = viewBoxAttr.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 4)
                            {
                                float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out viewBoxX);
                                float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out viewBoxY);
                                float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out viewBoxWidth);
                                float.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out viewBoxHeight);
                            }
                        }
                    }
                    else
                    {
                        int widthIdx = FindAttributeIndex(svgTag, "width");
                        int heightIdx = FindAttributeIndex(svgTag, "height");
                        if (widthIdx != -1)
                        {
                            string widthAttr = ExtractAttributeValue(svgTag, widthIdx);
                            if (widthAttr != null)
                                float.TryParse(widthAttr.Replace("px", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out viewBoxWidth);
                        }
                        if (heightIdx != -1)
                        {
                            string heightAttr = ExtractAttributeValue(svgTag, heightIdx);
                            if (heightAttr != null)
                                float.TryParse(heightAttr.Replace("px", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out viewBoxHeight);
                        }
                    }
                }
            }

            string initialPath = string.Join(" ", pathDatas);

            Windows.UI.Xaml.Media.Geometry xamlGeom = Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Windows.UI.Xaml.Media.Geometry), initialPath) as Windows.UI.Xaml.Media.Geometry;
            Rect exactBounds = xamlGeom?.Bounds ?? Rect.Empty;

            if (viewBoxWidth <= 0) viewBoxWidth = (float)exactBounds.Width;
            if (viewBoxHeight <= 0) viewBoxHeight = (float)exactBounds.Height;

            string viewBox = viewBoxWidth > 0 && viewBoxHeight > 0
                ? $"{viewBoxX.ToString(CultureInfo.InvariantCulture)} {viewBoxY.ToString(CultureInfo.InvariantCulture)} {viewBoxWidth.ToString(CultureInfo.InvariantCulture)} {viewBoxHeight.ToString(CultureInfo.InvariantCulture)}"
                : "0 0 1024 1024";

            string newSvg = $"<svg viewBox=\"{viewBox}\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"{initialPath}\" fill=\"black\" /></svg>";

            StorageFile tempFile = await StorageHelper.CreateTempFileAsync($"SVGP\\{Guid.NewGuid()}.svg").AsTask().ConfigureAwait(false);
            await FileIO.WriteTextAsync(tempFile, newSvg).AsTask().ConfigureAwait(false);

            TrueTypeGlyphReceiver receiver = new();
            foreach (var d in pathDatas)
            {
                try
                {
                    SvgPathParser.Parse(d, receiver);
                }
                catch { }
            }

            float dx = -viewBoxX;
            float dy = -(viewBoxY + viewBoxHeight);

            List<IReadOnlyList<Vector2>> contourViews = [];
            foreach (var c in receiver.Contours)
            {
                for (int i = 0; i < c.Count; i++)
                {
                    Vector2 pt = c[i];
                    c[i] = new Vector2(pt.X + dx, pt.Y + dy);
                }
                contourViews.Add(c.AsReadOnly());
            }

            Windows.Foundation.Rect bounds = new(exactBounds.X + dx, exactBounds.Y + dy, exactBounds.Width, exactBounds.Height);
            DWriteGlyphOutline outline = new(contourViews.AsReadOnly(), receiver.PointOnCurve.AsReadOnly(), bounds);

            float glyphScale = viewBoxHeight > 0 ? 1024f / viewBoxHeight : 1f;
            float advanceWidth = viewBoxWidth; // Keep in original coordinate space!

            Character svgChar = new(nextPUA);
            FontGlyph glyph = new(null, svgChar, metrics: new(Scale: glyphScale, CustomAdvanceWidth: advanceWidth), CustomOutline: outline, CustomImagePath: tempFile.GetAppPath());
            return glyph;
        }
        catch (Exception ex)
        {
            Utils.AppendDiagnostics("SVGHelper TryLoadFontGlyph", ex);
        }

        return null;
    }

    #region Path Extraction

    private static List<string> ExtractPaths(string svgText)
    {
        List<string> pathDatas = new();
        int pos = 0;
        bool inDefs = false;
        bool inClipPath = false;

        while (pos < svgText.Length)
        {
            while (pos < svgText.Length && char.IsWhiteSpace(svgText[pos]))
                pos++;

            if (pos >= svgText.Length)
                break;

            if (svgText[pos] == '<')
            {
                if (pos + 1 < svgText.Length && svgText[pos + 1] == '/')
                {
                    if (StartsWithIgnoreCase(svgText, pos + 2, "defs"))
                    {
                        inDefs = false;
                        pos = svgText.IndexOf('>', pos) + 1;
                        continue;
                    }
                    if (StartsWithIgnoreCase(svgText, pos + 2, "clipPath"))
                    {
                        inClipPath = false;
                        pos = svgText.IndexOf('>', pos) + 1;
                        continue;
                    }
                }

                if (StartsWithIgnoreCase(svgText, pos + 1, "defs"))
                {
                    int tagEnd = svgText.IndexOf('>', pos);
                    if (tagEnd != -1)
                    {
                        if (tagEnd > 0 && svgText[tagEnd - 1] != '/')
                            inDefs = true;
                        pos = tagEnd + 1;
                        continue;
                    }
                }
                if (StartsWithIgnoreCase(svgText, pos + 1, "clipPath"))
                {
                    int tagEnd = svgText.IndexOf('>', pos);
                    if (tagEnd != -1)
                    {
                        if (tagEnd > 0 && svgText[tagEnd - 1] != '/')
                            inClipPath = true;
                        pos = tagEnd + 1;
                        continue;
                    }
                }

                if (StartsWithIgnoreCase(svgText, pos + 1, "path"))
                {
                    int nextCharIndex = pos + 5;
                    if (nextCharIndex < svgText.Length)
                    {
                        char nextChar = svgText[nextCharIndex];
                        if (char.IsWhiteSpace(nextChar) || nextChar == '/' || nextChar == '>')
                        {
                            int tagEnd = svgText.IndexOf('>', pos);
                            if (tagEnd != -1)
                            {
                                if (!inDefs && !inClipPath)
                                {
                                    string pathTag = svgText.Substring(pos, tagEnd - pos);
                                    if (!IsInvisible(pathTag))
                                    {
                                        int dIndex = FindAttributeIndex(pathTag, "d");
                                        if (dIndex != -1)
                                        {
                                            string dValue = ExtractAttributeValue(pathTag, dIndex);
                                            if (!string.IsNullOrWhiteSpace(dValue))
                                                pathDatas.Add(dValue);
                                        }
                                    }
                                }
                                pos = tagEnd + 1;
                                continue;
                            }
                        }
                    }
                }
            }

            pos++;
        }

        return pathDatas;
    }

    private static bool StartsWithIgnoreCase(string s, int start, string sub)
    {
        if (start + sub.Length > s.Length)
            return false;

        for (int i = 0; i < sub.Length; i++)
        {
            if (char.ToLowerInvariant(s[start + i]) != char.ToLowerInvariant(sub[i]))
                return false;
        }

        int next = start + sub.Length;
        if (next < s.Length)
        {
            char c = s[next];
            return char.IsWhiteSpace(c) || c == '/' || c == '>';
        }

        return true;
    }

    private static bool IsInvisible(string pathTag)
    {
        int fillIdx = FindAttributeIndex(pathTag, "fill");
        if (fillIdx != -1)
        {
            string fillVal = ExtractAttributeValue(pathTag, fillIdx);
            if (fillVal != null && fillVal.Equals("none", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        int displayIdx = FindAttributeIndex(pathTag, "display");
        if (displayIdx != -1)
        {
            string displayVal = ExtractAttributeValue(pathTag, displayIdx);
            if (displayVal != null && displayVal.Equals("none", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        int styleIdx = FindAttributeIndex(pathTag, "style");
        if (styleIdx != -1)
        {
            string styleVal = ExtractAttributeValue(pathTag, styleIdx);
            if (styleVal != null)
            {
                string cleanStyle = styleVal.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "").ToLowerInvariant();
                if (cleanStyle.Contains("fill:none") || cleanStyle.Contains("display:none"))
                    return true;
            }
        }

        return false;
    }

    private static int FindAttributeIndex(string tag, string attrName)
    {
        int index = 0;
        while (true)
        {
            index = tag.IndexOf(attrName, index, StringComparison.Ordinal);
            if (index == -1)
                return -1;

            if (index > 0 && char.IsWhiteSpace(tag[index - 1]))
            {
                int equalsIndex = index + attrName.Length;
                while (equalsIndex < tag.Length && char.IsWhiteSpace(tag[equalsIndex]))
                    equalsIndex++;

                if (equalsIndex < tag.Length && tag[equalsIndex] == '=')
                    return index;
            }

            index += attrName.Length;
        }
    }

    private static string ExtractAttributeValue(string tag, int attrIndex)
    {
        int pos = tag.IndexOf('=', attrIndex);
        if (pos == -1)
            return null;

        pos++;
        while (pos < tag.Length && char.IsWhiteSpace(tag[pos]))
            pos++;

        if (pos >= tag.Length)
            return null;

        char quote = tag[pos];
        if (quote != '"' && quote != '\'')
        {
            int start = pos;
            while (pos < tag.Length && !char.IsWhiteSpace(tag[pos]) && tag[pos] != '/' && tag[pos] != '>')
                pos++;
            return tag.Substring(start, pos - start);
        }

        pos++;
        int valStart = pos;
        int valEnd = tag.IndexOf(quote, pos);
        if (valEnd == -1)
            return null;

        return tag.Substring(valStart, valEnd - valStart);
    }

    #endregion

    #endregion




    //------------------------------------------------------
    //
    //  SVG Glyph Extraction
    //
    //------------------------------------------------------

    #region Glyph Extraction

    public static string FilterSVGToGlyph(int glyphIndex, IBuffer svgBuffer)
    {
        string svgStr = ReadSVGBuffer(svgBuffer);

        // Delegate to FilterSVGToGlyph
        try
        {
            return FilterSVGToGlyph(glyphIndex, svgStr);
        }
        catch
        {
            return svgStr;
        }
    }

    /// <summary>
    /// Reads an SVG character buffer into a string, decompressing it if needed
    /// </summary>
    /// <param name="svgBuffer"></param>
    /// <returns></returns>
    public static string ReadSVGBuffer(IBuffer svgBuffer)
    {
        string svgStr;

        // 1. Decompress gzip-compressed SVG if needed
        if (svgBuffer.Length > 2 && svgBuffer.GetByte(0) == 31 && svgBuffer.GetByte(1) == 139)
        {
            using var ms = svgBuffer.AsStream();
            using var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new System.IO.StreamReader(gzip);
            svgStr = reader.ReadToEnd();
        }
        else
        {
            using var dataReader = DataReader.FromBuffer(svgBuffer);
            dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            svgStr = dataReader.ReadString(svgBuffer.Length);
        }

        // 2. Strip <?xml ... ?> header
        if (svgStr.StartsWith("<?xml"))
            svgStr = svgStr.Remove(0, svgStr.IndexOf('>') + 1);
        svgStr = svgStr.TrimStart();

        return svgStr;
    }

    public static string FilterSVGToGlyph(int targetGlyphIndex, string str)
    {
        /* 
         * Some fonts do horrible things like embed a super SVG file that contains all of the 
         * glyphs inside, split into multiple in-congruent parts.
         * They might even, if they truly hate performance and memory efficiency, compress this SVG file, 
         * requiring the font renderer to read the entire thing into memory, decompress it, and then manually 
         * figure out all the separate parts of this file required to render a single glyph.
         * We need to do all of this manually to extract the raw SVG components for our export feature.
         * Oh joy.
         */

        XmlDocument xmlDoc = new();
        xmlDoc.LoadXml(str);
        string targetId = $"glyph{targetGlyphIndex}";

        XmlElement targetElement = xmlDoc.GetElementsByTagName("*")
            .OfType<XmlElement>()
            .FirstOrDefault(e => e.GetAttribute("id") is string s && (s == targetId || s == $"{targetId}.0"));

        if (targetElement != null && targetElement != xmlDoc.DocumentElement)
        {
            XmlElement root = xmlDoc.DocumentElement;
            List<IXmlNode> otherGlyphGroups = root.ChildNodes
                .Where(e => e.NodeName.Equals("g", StringComparison.OrdinalIgnoreCase) && e != targetElement)
                .ToList();

            foreach (IXmlNode el in otherGlyphGroups)
                root.RemoveChild(el);

            HashSet<string> usedIds = [];
            void ScanXmlText(string xmlText)
            {
                if (string.IsNullOrEmpty(xmlText)) return;
                MatchCollection matches = Regex.Matches(xmlText, @"#([A-Za-z0-9_\-\.]+)");
                foreach (Match m in matches)
                    usedIds.Add(m.Groups[1].Value);
            }

            ScanXmlText(targetElement.GetXml());

            IXmlNode defs = xmlDoc.GetElementsByTagName("defs").FirstOrDefault();
            if (defs != null)
            {
                bool added = true;
                while (added)
                {
                    int countBefore = usedIds.Count;
                    foreach (XmlElement defChild in defs.ChildNodes.OfType<XmlElement>())
                    {
                        string id = defChild.GetAttribute("id");
                        if (!string.IsNullOrEmpty(id) && usedIds.Contains(id))
                            ScanXmlText(defChild.GetXml());
                    }
                    added = usedIds.Count > countBefore;
                }

                if (usedIds.Count > 0)
                {
                    List<IXmlNode> defsToRemove = defs.ChildNodes
                        .OfType<XmlElement>()
                        .Where(e => e.GetAttribute("id") is string id && !string.IsNullOrEmpty(id) && !usedIds.Contains(id))
                        .Cast<IXmlNode>()
                        .ToList();

                    foreach (IXmlNode el in defsToRemove)
                        defs.RemoveChild(el);
                }
            }

            str = xmlDoc.GetXml();
        }

        return str;
    }


    public static string FitBounds(string svg, float emSize)
    {
        return DirectWrite.FitSvgBounds(svg, emSize);
    }

    #endregion

}