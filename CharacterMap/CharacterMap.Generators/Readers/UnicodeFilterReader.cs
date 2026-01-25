using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

[Generator]
public class UnicodeFilterReader : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax cds && cds.Identifier.ValueText == "UnicodeRanges",
                transform: (ctx, _) => ctx.Node as ClassDeclarationSyntax
            )
            .Where(cds => cds is not null)
            .Collect();

        context.RegisterSourceOutput(classDeclarations, (spc, nodes) =>
        {
            bool gen = false;
            StringBuilder sbu = new();
            StringBuilder sbf = new();

            sbu.AppendLine("" +
                "/// <summary>\r\n    /// Unicode Ranges sorted by Range\r\n    /// </summary>\r\n" +
                "    public static IReadOnlyList<NamedUnicodeRange> All { get; } = [");

            sbf.AppendLine("public static IReadOnlyList<BasicFontFilter> AllFilters { get; } = [");

            foreach (var u in nodes)
            {
                var filters = u.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .Where(f => f.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == "MakeBasicFilter")))
                    .ToList();

                foreach (var filter in filters)
                {
                    gen = true;
                    sbf.AppendLine(2, $"BasicFontFilter.ForNamedRange({filter.Declaration.Variables[0].Identifier.ValueText}),");
                    sbu.AppendLine(2, $"{filter.Declaration.Variables[0].Identifier.ValueText},");
                }
            }

            if (!gen)
                return;

            sbf.AppendLine(1, "];");
            sbu.AppendLine(1, "];");

            SourceText sourceText = SourceText.From(
$@"namespace CharacterMap.Models;

partial class UnicodeRanges
{{
    {sbf}

    {sbu}
}}", Encoding.UTF8);

            spc.AddSource("UnicodeRanges.g.cs", sourceText);
        });
    }
}
