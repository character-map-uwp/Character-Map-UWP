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
public class FileNameReader : SyntaxReader
{
    List<string> writers = [];

    public override void Read(IEnumerable<SyntaxNode> nodes)
    {
        foreach (var u in nodes.OfNamedClass("FileNameWriter"))
        {
            var writer = u.DescendantNodesAndSelf()
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => p.IsNamedType("FileNameWriter"))
                .ToList();

            foreach (var w in writer)
                writers.Add(w.Identifier.ValueText);
        }
    }

    public override void Write(GeneratorExecutionContext context)
    {
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

        context.AddSource("FileNameWriter.g.cs", src);

    }
}