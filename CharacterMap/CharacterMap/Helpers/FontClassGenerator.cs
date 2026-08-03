using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CharacterMap.Helpers;

public enum FontCodeOutputType
{
    /// <summary>
    /// Outputs integer hex constants usable as raw Unicode indices (e.g. public const int GlobalNavButton = 0xE700;)
    /// </summary>
    IntHex,

    /// <summary>
    /// Outputs encoded C# string constants directly usable in TextBlock controls (e.g. public const string GlobalNavButton = "\uE700";)
    /// </summary>
    EncodedString,

    /// <summary>
    /// Outputs character literals for BMP unicode characters (e.g. public const char GlobalNavButton = '\uE700';)
    /// </summary>
    CharLiteral
}

public class FontClassGenerator
{
    const string NAMESPACE_ID = "🐒👽😂";
    const string CLASSNAME_ID = "👌😘🔥";

    const string DEFAULT_CLASS_NAME = "IconFont";

    string baseString = null;

    /// <summary>
    /// Generates a source code class containing constants for the provided font glyphs.
    /// Currently C# only. Can be expanded to support other formats.
    /// </summary>
    public (string Content, String ClassName) Generate(
        string fontName, 
        string className,
        IEnumerable<FontGlyph> glyphs, 
        FontCodeOutputType outputType = FontCodeOutputType.IntHex,
        string namespaceName = null)
    {
        if (glyphs == null) 
            return (string.Empty, string.Empty);

        baseString ??= BuildBaseString(fontName, glyphs, outputType, namespaceName);

        var name = !string.IsNullOrWhiteSpace(className)
                        ? SanitizeClassName(className)
                        : SanitizeClassName(fontName+"Icons");

        var output = baseString.Replace(CLASSNAME_ID, name);
        return (output, name);
    }

    private static string BuildBaseString(string fontName, IEnumerable<FontGlyph> glyphs, FontCodeOutputType outputType, string namespaceName)
    {
        StringBuilder sb = new();
        
        bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {NAMESPACE_ID};");
            sb.AppendLine();
        }

        sb.AppendLine($"public static class {CLASSNAME_ID}");
        sb.AppendLine($"{{");

        HashSet<string> usedMemberNames = new(StringComparer.Ordinal);
        foreach (FontGlyph glyph in glyphs)
        {
            if (glyph?.Character == null) continue;

            uint unicode = glyph.Character.UnicodeIndex;
            string memberName = GetValidMemberName(glyph, unicode, usedMemberNames);

            if (string.IsNullOrWhiteSpace(memberName))
                continue;

            string valueDeclaration = outputType switch
            {
                FontCodeOutputType.EncodedString => FormatStringLiteral(unicode),
                FontCodeOutputType.CharLiteral => FormatCharLiteral(unicode),
                _ => $"0x{unicode:X4}"
            };

            string typeName = outputType switch
            {
                FontCodeOutputType.EncodedString => "string",
                FontCodeOutputType.CharLiteral when unicode <= 0xFFFF => "char",
                FontCodeOutputType.CharLiteral => "string",
                _ => "int"
            };

            sb.AppendLine($"    public const {typeName} {memberName} = {valueDeclaration};");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string SanitizeClassName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return DEFAULT_CLASS_NAME;

        // Remove non-alphanumeric chars and build PascalCase name
        string[] words = Regex.Split(rawName, @"[^a-zA-Z0-9]+");
        StringBuilder sb = new();
        foreach (string w in words)
        {
            if (w.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1)
                    sb.Append(w.Substring(1));
            }
        }

        string result = sb.ToString();
        if (string.IsNullOrEmpty(result)) return DEFAULT_CLASS_NAME;

        //if (!result.EndsWith("Icons", StringComparison.OrdinalIgnoreCase))
        //    result += "Icons";

        if (char.IsDigit(result[0]))
            result = "Icon" + result;

        return result;
    }

    private static string GetValidMemberName(FontGlyph glyph, uint unicode, HashSet<string> usedNames)
    {
        string rawName = glyph.GlyphName;

        if (string.IsNullOrWhiteSpace(rawName) || rawName.Equals(".notdef", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Clean identifier characters
        string sanitized = Regex.Replace(rawName, @"[^a-zA-Z0-9_]", "");

        // Ensure does not start with digit
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        // Ensure unique within the class
        string finalName = sanitized;
        int count = 1;
        while (!usedNames.Add(finalName))
        {
            count++;
            finalName = $"{sanitized}{count}";
        }

        return finalName;
    }

    private static string FormatStringLiteral(uint unicode)
    {
        if (unicode <= 0xFFFF)
            return $"\"\\u{unicode:X4}\"";

        string utf32 = char.ConvertFromUtf32((int)unicode);
        return $"\"\\u{(ushort)utf32[0]:X4}\\u{(ushort)utf32[1]:X4}\"";
    }

    private static string FormatCharLiteral(uint unicode)
    {
        if (unicode <= 0xFFFF)
            return $"'\\u{unicode:X4}'";

        // Surrogate pair fallback to string literal
        string utf32 = char.ConvertFromUtf32((int)unicode);
        return $"\"\\u{(ushort)utf32[0]:X4}\\u{(ushort)utf32[1]:X4}\"";
    }
}
