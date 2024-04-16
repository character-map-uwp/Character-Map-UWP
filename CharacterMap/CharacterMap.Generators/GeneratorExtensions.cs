using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace CharacterMap.Generators;

public static class GeneratorExtensions
{
    public static bool HasAttribute<T>(this T field, string name) where T: MemberDeclarationSyntax
    {
        return field.AttributeLists.Any(t => t.Attributes.Any(a => a.Name is IdentifierNameSyntax i && i.Identifier.ValueText.ToString() == name));
    }

    public static string GetQualifiedName(this ClassDeclarationSyntax c)
    {
        return $"{((NamespaceDeclarationSyntax)c.Parent).Name}.{c.Identifier}";
    }

    public static string GetNamespace(this ClassDeclarationSyntax c)
    {
        return $"{((NamespaceDeclarationSyntax)c.Parent).Name}";
    }

    public static IEnumerable<ITypeSymbol> GetBaseTypes(this ITypeSymbol start)
    {
        Queue<ITypeSymbol> queue = [];
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            var child = item.BaseType;
            if (child is not null)
            {
                yield return child;
                queue.Enqueue(child);
            }
        }
    }
}
