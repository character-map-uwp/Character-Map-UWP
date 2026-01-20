using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

[Generator]
public class AttachedPropertyReader : IIncrementalGenerator
{
    string TEMPLATE =
        "    public static {2} Get{0}(DependencyObject obj) => ({2})obj.GetValue({0}Property);\r\n\r\n" +
        "    public static void Set{0}(DependencyObject obj, {2} value) => obj.SetValue({0}Property, value);\r\n\r\n" +
        "    public static readonly DependencyProperty {0}Property =\r\n" +
        "        DependencyProperty.RegisterAttached(\"{0}\", typeof({2}), typeof({1}), new PropertyMetadata({3}, (d,e) => On{0}Changed(d,e)));\r\n\r\n" +
        "    static partial void On{0}Changed(DependencyObject d, DependencyPropertyChangedEventArgs e);\r\n";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax cds && cds.AttributeLists.Any(),
                transform: static (ctx, _) => ctx.Node as ClassDeclarationSyntax)
            .Where(static c => c is not null);

        var dpData = classDeclarations.Select((n, _) =>
        {
            if (!n.HasGenericAttribute("AttachedProperty"))
                return null;

            DPData src = new()
            {
                ParentClass = n.Identifier.ValueText,
                ParentNamespace = n.GetNamespace(),
                Usings = (n.Parent?.Parent as CompilationUnitSyntax)?.Usings.Select(u => $"using {u.Name.ToString()};")?.ToList() ?? new()
            };

            List<DPData> dataList = new();
            foreach (var a in n.AttributeLists
                               .SelectMany(s => s.Attributes)
                               .Where(a => a.Name.ToString().StartsWith("AttachedProperty")))
            {
                string type = null;
                if (a.Name is GenericNameSyntax gen)
                    type = gen.TypeArgumentList.Arguments[0].ToString();

                var d = a.GetArgument("Name") is { } na && na.NameEquals is { } ne
                    ? src with
                    {
                        Name = a.GetArgument("Name")?.GetValue(),
                        Default = a.GetArgument("Default")?.GetValue() ?? "default",
                        Type = type ?? a.GetArgument("Type")?.GetValue()?.Replace("typeof(", string.Empty).Replace(")", string.Empty) ?? "object"
                    }
                    : src with
                    {
                        Name = a.ArgumentList?.Arguments[0].GetValue() ?? type,
                        Type = type ?? "object",
                        Default = a.ArgumentList?.Arguments.Skip(1)?.FirstOrDefault()?.GetValue() ?? "default"
                    };

                dataList.Add(d);
            }
            return dataList;
        })
        .Where(list => list is not null)
        .SelectMany((list, _) => list!);

        context.RegisterSourceOutput(dpData.Collect(), (spc, data) =>
        {
            var groups = data.GroupBy(d => $"{d.ParentNamespace}.{d.ParentClass}.g.ap.cs");
            foreach (var group in groups)
            {
                string file = group.Key;
                string ns = group.First().ParentNamespace;
                string target = group.First().ParentClass;
                List<string> usings = group.First().Usings;

                StringBuilder sb = new();

                foreach (DPData dp in group)
                    sb.AppendLine(
                        string.Format(TEMPLATE, dp.Name, dp.ParentClass, dp.Type, dp.GetDefault(), dp.GetCastType("e.OldValue"), dp.GetCastType("e.NewValue")));

                var s = sb.ToString();
                spc.AddSource(file, SourceText.From(
$@"{string.Join("\r\n", usings)}

namespace {ns};

partial class {target}
{{
{sb}
}}", Encoding.UTF8));
            }
        });
    }
}