using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

public class UnicodeFilterReader : SyntaxReader
{
    private bool gen = false;
    StringBuilder sbu = new();
    StringBuilder sbf = new();

    public override void Read(IEnumerable<SyntaxNode> nodes)
    {
        sbu.AppendLine("" +
            "/// <summary>\r\n    /// Unicode Ranges sorted by Range\r\n    /// </summary>\r\n" +
            "    public static IReadOnlyList<NamedUnicodeRange> All { get; } = [");

        sbf.AppendLine("public static IReadOnlyList<BasicFontFilter> AllFilters { get; } = [");

        foreach (var u in nodes.OfNamedClass("UnicodeRanges"))
        {
            var filters = u.DescendantNodesAndSelf()
                .OfType<FieldDeclarationSyntax>()
                .Where(f => f.HasAttribute("MakeBasicFilter"))
                .ToList();

            foreach (var filter in filters)
            {
                gen = true;
                sbf.AppendLine(2, $"BasicFontFilter.ForNamedRange({filter.Declaration.Variables[0].Identifier.ValueText}),");
                sbu.AppendLine(2, $"{filter.Declaration.Variables[0].Identifier.ValueText},");
            }
        }
    }

    public override void Write(GeneratorExecutionContext context)
    {
        if (gen is false)
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

        context.AddSource($"UnicodeRanges.g.cs", sourceText);
    }
}
