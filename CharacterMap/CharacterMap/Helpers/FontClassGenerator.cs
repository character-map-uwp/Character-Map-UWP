using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CharacterMap.Helpers;

public class FontClassGenerator
{
    CodeTemplateOption _previousTemplate = null;
    string _cached = null;

    public (string Content, string OutputClassName) ProcessTemplate(
        CodeTemplateOption template,
        string fontName,
        string className,
        string namespaceName,
        IEnumerable<FontGlyph> glyphs,
        bool forceRegenerate = false)
    {
        if (template is null) return (string.Empty, string.Empty);

        if (_previousTemplate != template || forceRegenerate)
            _cached = null;
        
        _previousTemplate = template;

        List<FontGlyph> glyphList = [.. (glyphs ?? [])];
        string cleanClassName = string.IsNullOrWhiteSpace(className) ? SanitizeIdentifier(fontName) : className;

        // 1. Process Loop Blocks: {{#Glyphs}} ... {{/Glyphs}}
        string result = _cached ??= Regex.Replace(template.Template, @"[ \t]*\{\{#Glyphs\}\}\r?\n?([\s\S]*?)[ \t]*\{\{/Glyphs\}\}\r?\n?", match =>
        {
            string loopBody = match.Groups[1].Value;
            StringBuilder sb = new();
            HashSet<string> usedMemberNames = new(StringComparer.Ordinal);

            for (int i = 0; i < glyphList.Count; i++)
            {
                FontGlyph g = glyphList[i];
                if (g?.Character == null) continue;

                uint unicode = g.Character.UnicodeIndex;
                string rawName = string.IsNullOrWhiteSpace(g.GlyphName) ? null : g.GlyphName;
                if (rawName == null)
                    continue;

                string cleanName = GetUniqueMemberName(rawName, usedMemberNames);

                string expandedLine = EvaluateTags(loopBody, name => name switch
                {
                    "Name" => cleanName,
                    "RawName" => rawName,
                    "UnicodeIndex" => unicode.ToString(),
                    "Index" => i.ToString(),
                    "IsLast" => (i == glyphList.Count - 1).ToString().ToLowerInvariant(),
                    _ => null
                });

                sb.Append(expandedLine);
            }

            return sb.ToString();
        });

        // 2. Process Global Tags
        result = EvaluateTags(result, name => name switch
        {
            "FontName" => fontName ?? string.Empty,
            "ClassName" => cleanClassName,
            "Namespace" => namespaceName ?? string.Empty,
            "GlyphCount" => glyphList.Count.ToString(),
            "GeneratedDate" => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            _ => null
        });

        // 3. Clean up empty namespace blocks if namespace is omitted
        if (string.IsNullOrWhiteSpace(namespaceName))
            result = Regex.Replace(result, @"[ \t]*\{\{#Namespace\}\}[\s\S]*?\{\{/Namespace\}\}\r?\n?", string.Empty);
        else
            result = Regex.Replace(result, @"[ \t]*\{\{/?Namespace\}\}\r?\n?", string.Empty);

        return (result, cleanClassName);
    }

    private static string EvaluateTags(string input, Func<string, string> valueProvider)
    {
        return Regex.Replace(input, @"\{\{\s*([a-zA-Z0-9_]+)(?:\s*\|\s*([a-zA-Z0-9_]+))?\s*\}\}", match =>
        {
            string varName = match.Groups[1].Value;
            string modifier = match.Groups[2].Success ? match.Groups[2].Value : null;

            string rawValue = valueProvider(varName);
            if (rawValue == null) return match.Value; // Leave unhandled tags as-is

            return ApplyModifier(rawValue, modifier);
        });
    }

    private static string ApplyModifier(string val, string modifier)
    {
        if (string.IsNullOrEmpty(modifier)) return val;

        bool isNumeric = uint.TryParse(val, out uint unicode);

        return modifier.ToLowerInvariant() switch
        {
            // Number / Codepoint Modifiers
            "hex" when isNumeric => $"{unicode:X4}",
            "hex_0x" when isNumeric => $"0x{unicode:X4}",
            "hex_0x_8" when isNumeric => $"0x{unicode:X8}",
            "utf16_escape" when isNumeric => FormatUtf16Escape(unicode),
            "utf32_escape" when isNumeric => $"\\U{unicode:X8}",
            "char" when isNumeric => char.ConvertFromUtf32((int)unicode),

            // Text Identifier Modifiers
            "pascal_case" => ToPascalCase(val),
            "camel_case" => ToCamelCase(val),
            "snake_case" => ToSnakeCase(val),
            "upper_snake_case" => ToSnakeCase(val).ToUpperInvariant(),
            "upper_case" => val.ToUpperInvariant(),
            "lower_case" => val.ToLowerInvariant(),

            _ => val
        };
    }

    private static string FormatUtf16Escape(uint unicode)
    {
        if (unicode <= 0xFFFF) return $"\\u{unicode:X4}";
        string pair = char.ConvertFromUtf32((int)unicode);
        return $"\\u{(ushort)pair[0]:X4}\\u{(ushort)pair[1]:X4}";
    }

    private static string ToPascalCase(string str)
    {
        string[] words = Regex.Split(str ?? "", @"[^a-zA-Z0-9]+");
        StringBuilder sb = new();
        foreach (string w in words)
            if (w.Length > 0)
                sb.Append(char.ToUpperInvariant(w[0])).Append(w.Substring(1));

        string result = sb.ToString();
        if (string.IsNullOrEmpty(result)) return "Icon";
        if (char.IsDigit(result[0])) result = "_" + result;
        return result;
    }

    private static string ToCamelCase(string str)
    {
        string pascal = ToPascalCase(str);
        if (pascal.Length == 0) return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
    }

    private static string ToSnakeCase(string str)
    {
        string clean = Regex.Replace(str ?? "", @"[^a-zA-Z0-9]+", "_").Trim('_');
        return Regex.Replace(clean, @"(?<!^)(?=[A-Z])", "_").ToLowerInvariant();
    }

    private static string SanitizeIdentifier(string str) => ToPascalCase(str);

    private static string GetUniqueMemberName(string rawName, HashSet<string> used)
    {
        string baseName = ToPascalCase(rawName);
        string name = baseName;
        int count = 1;
        while (!used.Add(name))
        {
            count++;
            name = $"{baseName}{count}";
        }
        return name;
    }
}

public record CodeTemplateOption(
    string Language,
    string Name,
    string Template,
    string FileExtension = ".cs")
{
    public string DisplayName => $"{Language} - {Name}";
}

public class CodeTemplates
{
    public static IReadOnlyList<CodeTemplateOption> Options => field ??= [
        new("C#", "Encoded String", CSharpTemplate.EncodedString, ".cs"),
        new("C#", "Char Literal", CSharpTemplate.CharLiteral, ".cs"),
        new("C#", "Integer Hex", CSharpTemplate.UnicodeIndex, ".cs"),
        new("TypeScript", "Enum", TypescriptTemplate.Enum, ".ts"),
        new("TypeScript", "Const Object", TypescriptTemplate.ConstObject, ".ts"),
        new("C++", "Header Struct", CppTemplate.Header, ".h"),
        new("Python", "Enum", PythonTemplate.Enum, ".py"),
        new("Rust", "Module", RustTemplate.Module, ".rs"),
        new("Dart / Flutter", "IconData Class", DartTemplate.IconData, ".dart"),
        new("Kotlin", "Object", KotlinTemplate.Object, ".kt"),
        new("Kotlin", "Enum", KotlinTemplate.Enum, ".kt"),
        new("XAML", "ResourceDictionary", XamlTemplate.ResourceDictionary, ".xaml"),
    ];

    public static class CSharpTemplate
    {
        public const string UnicodeIndex = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}
            // Font: {{FontName}} ({{GlyphCount}} glyphs)

            {{#Namespace}}
            namespace {{Namespace}};
            {{/Namespace}}

            public static class {{ClassName}}
            {
            {{#Glyphs}}
                public const int {{Name | pascal_case}} = {{UnicodeIndex | hex_0x}};
            {{/Glyphs}}
            }
            """;

        public const string EncodedString = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}
            {{#Namespace}}
            namespace {{Namespace}};
            {{/Namespace}}

            public static class {{ClassName}}
            {
            {{#Glyphs}}
                public const string {{Name | pascal_case}} = "{{UnicodeIndex | utf16_escape}}";
            {{/Glyphs}}
            }
            """;

        public const string CharLiteral = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}

            {{#Namespace}}
            namespace {{Namespace}};
            {{/Namespace}}

            public static class {{ClassName}}
            {
            {{#Glyphs}}
                public const char {{Name | pascal_case}} = '{{UnicodeIndex | utf16_escape}}';
            {{/Glyphs}}
            }
            """;
    }

    public static class TypescriptTemplate
    {
        public const string Enum = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}
            export enum {{ClassName}} {
            {{#Glyphs}}
              {{Name | pascal_case}} = "{{UnicodeIndex | utf16_escape}}",
            {{/Glyphs}}
            }
            """;

        public const string ConstObject = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}
            export const {{ClassName}} = {
            {{#Glyphs}}
              {{Name | pascal_case}}: "{{UnicodeIndex | utf16_escape}}",
            {{/Glyphs}}
            } as const;
            """;
    }

    public static class CppTemplate
    {
        public const string Header = """
            #pragma once
            // Auto-generated by Character Map UWP on {{GeneratedDate}}

            {{#Namespace}}
            namespace {{Namespace}} {
            {{/Namespace}}
                struct {{ClassName}} {
                {{#Glyphs}}
                    static constexpr char32_t {{Name | snake_case}} = {{UnicodeIndex | hex_0x}};
                {{/Glyphs}}
                };
            {{#Namespace}}
            }
            {{/Namespace}}
            """;
    }

    public static class PythonTemplate
    {
        public const string Enum = """
            # Auto-generated by Character Map UWP on {{GeneratedDate}}
            from enum import Enum

            class {{ClassName}}(str, Enum):
            {{#Glyphs}}
                {{Name | upper_snake_case}} = "{{UnicodeIndex | utf16_escape}}"
            {{/Glyphs}}
            """;
    }

    public static class RustTemplate
    {
        public const string Module = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}
            pub mod {{ClassName | snake_case}} {
            {{#Glyphs}}
                pub const {{Name | upper_snake_case}}: char = '{{UnicodeIndex | utf16_escape}}';
            {{/Glyphs}}
            }
            """;
    }

    public static class DartTemplate
    {
        public const string IconData = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}
            import 'package:flutter/widgets.dart';

            class {{ClassName}} {
              {{ClassName}}._();

              static const String _fontFamily = '{{FontName}}';

            {{#Glyphs}}
              static const IconData {{Name | camel_case}} = IconData({{UnicodeIndex | hex_0x}}, fontFamily: _fontFamily);
            {{/Glyphs}}
            }
            """;
    }

    public static class KotlinTemplate
    {
        public const string Object = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}

            {{#Namespace}}
            package {{Namespace}}
            {{/Namespace}}

            object {{ClassName}} {
            {{#Glyphs}}
                const val {{Name | upper_snake_case}}: Char = '{{UnicodeIndex | utf16_escape}}'
            {{/Glyphs}}
            }
            """;

        public const string Enum = """
            // Auto-generated by Character Map UWP on {{GeneratedDate}}

            {{#Namespace}}
            package {{Namespace}}
            {{/Namespace}}

            enum class {{ClassName}}(val code: Char) {
            {{#Glyphs}}
                {{Name | upper_snake_case}}('{{UnicodeIndex | utf16_escape}}'),
            {{/Glyphs}}
            }
            """;
    }

    public static class XamlTemplate
    {
        public const string ResourceDictionary = """
            <!-- Auto-generated by Character Map UWP on {{GeneratedDate}} -->
            <ResourceDictionary
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            {{#Glyphs}}
                <x:String x:Key="{{Name | pascal_case}}">&#x{{UnicodeIndex | hex}};</x:String>
            {{/Glyphs}}
            </ResourceDictionary>
            """;
    }
}