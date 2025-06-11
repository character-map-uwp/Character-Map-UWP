﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

internal record class DPData
{
    public string Default { get; init; }
    public string Container { get; init; }
    public string Callback { get; init; }
    public string Type { get; init; }
    public string Name { get; init; }
    public string TypeNamespace { get; init; }

    public string ParentClass { get; init; }
    public string ParentNamespace { get; init; }
    public List<string> Usings { get; init; }

    public string GetCastType(string value) => IsPrimitive(Type) ? $"({Type})({value} ?? ({Type})default)" : $"{value} as {Type}";

    public string GetDefault()
    {
        if (Type == "string" && Default != null
            && Default != "null"
            && Default != "default"
            && Default.StartsWith("nameof") is false)
            return $"\"{Default}\"";

        if (Default == "default")
            return $"default({Type})";

        return Default;
    }

    public string GetCallback()
    {
        if (!string.IsNullOrWhiteSpace(Callback) && !Callback.Contains("("))
            return $"{Callback}()";

        return Callback;
    }

    // Terrible way of doing this
    private bool IsPrimitive(string type)
    {
        return type is "bool" or "int" or "double" or "float" or "Duration" or "Color"
                or "Visibility" or "CornerRadius" or "CharacterCasing"
                or "FlowDirection" or "ContentPlacement" or "GridLength"
                or "Orientation" or "GlyphAnnotation" or "FlyoutPlacementMode"
                or "TextWrapping" or "Stretch" or "Thickness"
                or "TextAlignment" or "TimeSpan" or "BlendEffectMode"
                or "Point" or "TextLineBounds" or "MaterialCornerMode"
                or "ThemeIcon" or "SelectionVisualType" or "ZoomTriggerMode";
    }
}

public class DependencyPropertyReader : SyntaxReader
{
    string TEMPLATE =
        "    public {2} {0}\r\n" +
        "    {{\r\n" +
        "        get {{ return ({2})GetValue({0}Property); }}\r\n" +
        "        set {{ SetValue({0}Property, value); }}\r\n" +
        "    }}\r\n\r\n" +
        "    public static readonly DependencyProperty {0}Property =\r\n" +
        "        DependencyProperty.Register(nameof({0}), typeof({2}), typeof({1}), new PropertyMetadata({3}, (d, e) =>\r\n" +
        "        {{\r\n" +
        "            if (d is {1} o && e.NewValue is {2} i)\r\n" +
        //"            {{\r\n" +
        "                o.On{0}Changed({4}, {5});\r\n" +
        // "            }}\r\n" +
        "        }}));\r\n\r\n" +
        "    partial void On{0}Changed({2} o, {2} n);\r\n";

    string TEMPLATE2 =
        "    public {2} {0}\r\n" +
        "    {{\r\n" +
        "        get {{ return ({2})GetValue({0}Property); }}\r\n" +
        "        set {{ SetValue({0}Property, value); }}\r\n" +
        "    }}\r\n\r\n" +
        "    public static readonly DependencyProperty {0}Property =\r\n" +
        "        DependencyProperty.Register(nameof({0}), typeof({2}), typeof({1}), new PropertyMetadata({3}, (d, e) =>\r\n" +
        "        {{\r\n" +
        "            if (d is {1} o)\r\n" +
        "                o.{6};\r\n" +
        "        }}));\r\n\r\n";

    List<DPData> data = [];

    public override void Read(IEnumerable<SyntaxNode> nodes)
    {
        foreach (var n in nodes.OfType<ClassDeclarationSyntax>()
                               .Where(c => c.HasGenericAttribute("DependencyProperty")))
        {
            DPData src = new()
            {
                ParentClass = n.Identifier.ValueText,
                ParentNamespace = n.GetNamespace(),
                // Does not support static usings or using alias'
                Usings = (n.Parent?.Parent as CompilationUnitSyntax)?.Usings.Select(u => $"using {u.Name.ToString()};")?.ToList() ?? new()
            };

            foreach (var a in n.AttributeLists
                               .SelectMany(s => s.Attributes)
                               .Where(a => a.Name.ToString().StartsWith("DependencyProperty")))
            {
                string type = null;
                if (a.Name is GenericNameSyntax gen)
                    type = gen.TypeArgumentList.Arguments[0].ToString();

                var d = a.GetArgument("Name") is { } na && na.NameEquals is { } ne // Attribute property path,
                    ? src with
                    {
                        Name = a.GetArgument("Name")?.GetValue(),
                        Default = a.GetArgument("Default")?.GetValue() ?? "default",
                        Type = type ?? a.GetArgument("Type")?.GetValue()?.Replace("typeof(", string.Empty).Replace(")", string.Empty) ?? "object"
                    }
                    : src with // Constructor path - preferred
                    {
                        Name = a.ArgumentList.Arguments[0].GetValue(),
                        Type = type ?? "object",
                        Default = a.ArgumentList.Arguments.Skip(1)?.FirstOrDefault()?.GetValue() ?? "default",
                        Callback = FormatCallback(a.ArgumentList.Arguments.Skip(2)?.FirstOrDefault()?.GetValue() ?? null)
                    };

                static string FormatCallback(string input)
                {
                    if (input != null && input.StartsWith("nameof("))
                        input = input.Remove(0, "nameof(".Length)[0..^1];
                    return input;
                }

                data.Add(d);
            }
        }
    }

    public override void Write(GeneratorExecutionContext context)
    {
        if (data.Count == 0)
            return;

        foreach (var group in data.GroupBy(d => $"{d.ParentNamespace}.{d.ParentClass}.g.dp.cs"))
        {
            string file = group.Key;
            string ns = group.First().ParentNamespace;
            string target = group.First().ParentClass;
            List<string> usings = group.First().Usings;

            if (usings.Contains("using Windows.UI.Xaml;") is false)
                usings.Add("using Windows.UI.Xaml;");

            StringBuilder sb = new();

            foreach (DPData dp in group)
                sb.AppendLine(
                    string.Format(
                        string.IsNullOrWhiteSpace(dp.Callback) ? TEMPLATE : TEMPLATE2,
                        dp.Name, dp.ParentClass, dp.Type, dp.GetDefault(), dp.GetCastType("e.OldValue"), dp.GetCastType("e.NewValue"), dp.GetCallback()));

            var s = sb.ToString();
            context.AddSource(file, SourceText.From(
$@"{string.Join("\r\n", usings)}

namespace {ns};

partial class {target}
{{
{sb}
}}", Encoding.UTF8));
        }
    }
}
