using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

/// <summary>
/// Populates the "All" field in FileNameWriter by creating a list of all
/// static properties of type FileNameWriter in the FileNameWriter class.
/// </summary>
[Generator]
public class FileNameReader : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax cds && cds.Identifier.ValueText == "FileNameWriter",
                transform: (ctx, _) =>
                {
                    var classNode = (ClassDeclarationSyntax)ctx.Node;
                    var writers = classNode.DescendantNodes()
                        .OfType<PropertyDeclarationSyntax>()
                        .Where(p => p.Type is IdentifierNameSyntax id && id.Identifier.ValueText == "FileNameWriter")
                        .Select(p => p.Identifier.ValueText)
                        .ToList();
                    return writers;
                })
            .Where(writers => writers != null && writers.Count > 0)
            .Collect();

        context.RegisterSourceOutput(classDeclarations, (spc, writersList) =>
        {
            var writers = writersList.SelectMany(x => x).ToList();
            if (writers.Count == 0)
                return;

            string b = $"public static IReadOnlyList<FileNameWriter> All {{ get; }} = " +
                $"[\r\n        {string.Join(",\r\n        ", writers)}\r\n    ];";
            SourceText src = SourceText.From(
$@"namespace CharacterMap.Models;

partial class FileNameWriter
{{
    {b}
}}", Encoding.UTF8);

            spc.AddSource("FileNameWriter.g.cs", src);
        });
    }
}