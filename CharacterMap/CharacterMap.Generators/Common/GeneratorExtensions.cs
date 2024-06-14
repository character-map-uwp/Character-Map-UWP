using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators;

public static class GeneratorExtensions
{
    public static string Indent(this string s) => $"    {s}";

    public static AttributeArgumentSyntax GetArgument(this AttributeSyntax attribute, string name)
    {
        return attribute.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name?.Identifier.ValueText == name);
    }

    public static PropertyDeclarationSyntax GetProperty(this AttributeSyntax attribute, string name)
    {
        return attribute.DescendantNodesAndSelf().OfType<PropertyDeclarationSyntax>().FirstOrDefault(a => a.Identifier.ValueText == name);
    }


    public static string GetValue(this AttributeArgumentSyntax arg)
    {
        var s = arg.Expression.ToString();
        if (s.StartsWith("\"") && s.EndsWith("\""))
            return s[1..^1];

        return s;

        if (arg.Expression is TypeOfExpressionSyntax t && t.Type is PredefinedTypeSyntax p)
            return p.Keyword.ValueText;

        if (arg.Expression is TypeOfExpressionSyntax t1 && t1.Type is IdentifierNameSyntax i)
            return i.Identifier.ValueText;

        if (arg.Expression is MemberAccessExpressionSyntax m)
            return arg.Expression.ToString();

        if (arg.Expression is LiteralExpressionSyntax l)
            return l.Token.ValueText;

        return ((LiteralExpressionSyntax)arg.Expression).Token.ValueText;
    }

    public static StringBuilder AppendLine(this StringBuilder s, int indentLevel, string text)
    {
        for (int i = 0; i < indentLevel; i++)
            s.Append("    ");

        s.Append(text);
        s.AppendLine();
        return s;
    }

    public static IEnumerable<ClassDeclarationSyntax> OfNamedClass<T>
        (this IEnumerable<T> src, string name) where T: SyntaxNode
    {
        return src.OfType<ClassDeclarationSyntax>()
                  .Where(c => c.Identifier.ValueText == name);
    }

    public static bool HasAttribute<T>(this T field, string name) where T: MemberDeclarationSyntax
    {
        string full = $"{name}Attribute";
        return field.AttributeLists.Any(t => t.Attributes.Any(
            a => a.Name is IdentifierNameSyntax i 
            && (i.Identifier.ValueText == name || i.Identifier.ValueText == full)));
    }

    // TODO: Do this properly
    public static bool HasGenericAttribute<T>(this T field, string name) where T : MemberDeclarationSyntax
    {
        string full = $"{name}Attribute";
        return field.AttributeLists.Any(t => t.Attributes.Any(
            a => a.Name.ToString() is string s
            && (s.StartsWith(name) || s.StartsWith(full))));
    }

    public static bool IsNamedType<T>(this T field, string name) where T : BasePropertyDeclarationSyntax
    {
        return field.Type is IdentifierNameSyntax i && i.Identifier.ValueText == name;
    }

    public static string GetQualifiedName(this ClassDeclarationSyntax c)
    {
        return $"{((NamespaceDeclarationSyntax)c.Parent).Name}.{c.Identifier}";
    }

    public static string GetNamespace(this ClassDeclarationSyntax c)
    {
        if (c.Parent is FileScopedNamespaceDeclarationSyntax f)
            return f.Name.ToString(); 

        return ((NamespaceDeclarationSyntax)c.Parent).Name.ToString();
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
