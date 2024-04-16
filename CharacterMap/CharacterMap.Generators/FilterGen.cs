using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Schema;

namespace CharacterMap.Generators;

[Generator]
public class FilterGen : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        //Debug.WriteLine("Execute code generator");
        // Register a factory that can create our custom syntax receiver
        //context.RegisterForSyntaxNotifications(() => new UnicodeRangeFilter());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        //return;
        //Debugger.Launch();

        try
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol unicodeRanges = compilation.GetTypeByMetadataName("CharacterMap.Models.UnicodeRanges");
            //compilation.GetAtt
            Dictionary<string, ClassDeclaration> set = [];

            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                var semanticModel = context.Compilation.GetSemanticModel(tree);

                bool gen = false;

                StringBuilder sbu = new();
                sbu.AppendLine("" +
                    "/// <summary>\r\n    /// Unicode Ranges sorted by Range\r\n    /// </summary>\r\n" +
                    "public static IReadOnlyList<NamedUnicodeRange> All { get; } = [");

                StringBuilder sb = new();
                sb.AppendLine("    public static IReadOnlyList<BasicFontFilter> AllFilters { get; } = [");

                // 1. Handle Unicode Ranges
                foreach (var u in tree.GetRoot()
                  .DescendantNodesAndSelf()
                  .OfType<ClassDeclarationSyntax>()
                  .Where(c => c.Identifier.ValueText == "UnicodeRanges"))
                {
                    var filters = u.DescendantNodesAndSelf()
                        .OfType<FieldDeclarationSyntax>()
                        .Where(f => f.HasAttribute("MakeBasicFilter"))
                        .ToList();

                    foreach (var filter in filters)
                    {
                        gen = true;
                        sb.AppendLine($"        BasicFontFilter.ForNamedRange({(filter.Declaration.Variables[0].Identifier.ValueText)}),");
                        sbu.AppendLine($"       {filter.Declaration.Variables[0].Identifier.ValueText},");
                    }
                }

                if (gen)
                {
                    sb.AppendLine("        ];");
                    sbu.AppendLine("        ];");

                    var src = sb.ToString();

                    SourceText sourceText = SourceText.From(
            $@"namespace CharacterMap.Models;

partial class UnicodeRanges
{{
    {sb}

    {sbu}
}}", Encoding.UTF8);

                    context.AddSource($"UnicodeRanges.g.cs", sourceText);
                }

            }
        }
        catch (Exception ex)
        {

        }
    }

    public class UnicodeRangeFilter : ISyntaxReceiver
    {
        public ClassDeclarationSyntax Target { get; private set; }

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Find class called "UnicodeRanges"
            if (syntaxNode is ClassDeclarationSyntax { Identifier.ValueText: "UnicodeRanges" } cds)
            {
                //tds.AttributeLists.
            }
        }
    }
}