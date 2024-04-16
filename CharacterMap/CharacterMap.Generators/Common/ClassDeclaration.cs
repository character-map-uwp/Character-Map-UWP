using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CharacterMap.Generators;

/// <summary>
/// Gathers all declarations of a class over partial source files into a single unit
/// </summary>
[DebuggerDisplay("ClassDeclaration {QualifiedName}")]
public class ClassDeclaration
{
    public string QualifiedName { get; }
    public string Namespace { get; }
    public string Name { get; }

    private List<ClassDeclarationSyntax> _declarations { get; } = [];
    private List<ClassDeclarationSyntax> _generatedDeclarations { get; } = [];

    public ClassDeclaration(ClassDeclarationSyntax syntax)
    {
        QualifiedName = syntax.GetQualifiedName();
        Namespace = syntax.GetNamespace();
        Name = syntax.Identifier.ValueText;
        Add(syntax);
    }

    public bool Add(ClassDeclarationSyntax syntax)
    {
        if (QualifiedName != syntax.GetQualifiedName())
            return false;

        if (syntax.Parent.ToFullString().Contains("global::System.CodeDom.Compiler.GeneratedCodeAttribute"))
            _generatedDeclarations.Add(syntax);
        else
            _declarations.Add(syntax);

        return false;
    }

    public IEnumerable<ClassDeclarationSyntax> AllDeclarations() => Declarations.Concat(GeneratedDeclarations);

    /// <summary>
    /// User-defined declarations of the class
    /// </summary>
    public IReadOnlyList<ClassDeclarationSyntax> Declarations => _declarations;

    /// <summary>
    /// Auto-generated declarations of the class, e.g. from XAML compiler
    /// </summary>
    public IReadOnlyList<ClassDeclarationSyntax> GeneratedDeclarations => _generatedDeclarations;
}
