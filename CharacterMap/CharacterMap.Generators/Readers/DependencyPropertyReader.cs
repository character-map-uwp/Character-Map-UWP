using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

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
        "                o.On{0}Changed(e.OldValue as {4}, i);\r\n" +
       // "            }}\r\n" +
        "        }}));\r\n\r\n" +
        "    partial void On{0}Changed({4} oldValue, {2} newValue);\r\n";



    record class DPData
    {
        public string Default { get; init; }
        public string Container { get; init; }
        public string Type { get; init; }
        public string Name { get; init; }
        public string TypeNamespace { get; init; }

        public string ParentClass { get; init; }
        public string ParentNamespace { get; init; }
        public List<string> Usings { get; init; }

        public string GetCastType() => IsPrimitive(Type) ? $"{Type}?" : Type;

        // Terrible way of doing this
        private bool IsPrimitive(string type)
        {
            return type is "bool" or "int" or "double" or "float"
                    or "Visibility" or "CornerRadius" or "CharacterCasing"
                    or "FlowDirection" or "ContentPlacement" or "GridLength"
                    or "Orientation" or "GlyphAnnotation" or "FlyoutPlacementMode";
        }
    }

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
                        Default = a.ArgumentList.Arguments.Skip(1)?.FirstOrDefault()?.GetValue() ?? "default"
                    };


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

            StringBuilder sb = new ();

            foreach (DPData dp in group)
                sb.AppendLine(
                    string.Format(TEMPLATE, dp.Name, dp.ParentClass, dp.Type, dp.Default, dp.GetCastType()));

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
