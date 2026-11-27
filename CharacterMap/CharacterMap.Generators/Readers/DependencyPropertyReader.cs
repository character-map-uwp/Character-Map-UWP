using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

internal record class DPData
{
    public string Default { get; init; }
    public string Container { get; init; }
    public string Callback { get; init; }
    public string Type { get; init; }
    public string Name { get; init; }
    public string TypeNamespace { get; init; }

    public string ParentClass { get; init; }
    public string ParentNamespace { get; init; }
    public List<string> Usings { get; init; }
    public bool IsValueType { get; init; }
    public bool IsReadOnly { get; init; }
    public bool NotifyPropertyChanged { get; init; }
    public bool ImplementsINPC { get; init; }
    public bool HasArgsOnPropertyChanged { get; init; }
    public bool HasStringOnPropertyChanged { get; init; }
    public bool IsSealed { get; init; }

    public string GetCastType(string value) => IsValueType ? $"({Type})({value} ?? ({Type})default)" : $"{value} as {Type}";

    public string GetDefault()
    {
        if (Type == "string" && Default != null
            && Default != "null"
            && Default != "default"
            && Default.StartsWith("nameof") is false)
            return $"\"{Default}\"";

        if (Default == "default")
            return $"default({Type})";

        return Default;
    }

    public string GetCallback()
    {
        if (!string.IsNullOrWhiteSpace(Callback) && !Callback.Contains("("))
            return $"{Callback}()";

        return Callback;
    }

    public static bool IsPrimitiveFallback(string type)
    {
        return type is "bool" or "int" or "double" or "float" or "Duration" or "Color"
                or "Visibility" or "CornerRadius" or "CharacterCasing"
                or "FlowDirection" or "ContentPlacement" or "GridLength"
                or "Orientation" or "GlyphAnnotation" or "FlyoutPlacementMode"
                or "TextWrapping" or "Stretch" or "Thickness"
                or "TextAlignment" or "TimeSpan" or "BlendEffectMode"
                or "Point" or "TextLineBounds" or "MaterialCornerMode"
                or "ThemeIcon" or "SelectionVisualType" or "ZoomTriggerMode";
    }
}

[Generator]
public class DependencyPropertyReader : IIncrementalGenerator
{
    string TEMPLATE =
        "    public {2} {0}\r\n" +
        "    {{\r\n" +
        "        get {{ return ({2})GetValue({0}Property); }}\r\n" +
        "        {7} set {{ SetValue({0}Property, value); }}\r\n" +
        "    }}\r\n\r\n" +
        "    public static readonly DependencyProperty {0}Property =\r\n" +
        "        DependencyProperty.Register(nameof({0}), typeof({2}), typeof({1}), new PropertyMetadata({3}, (d, e) =>\r\n" +
        "        {{\r\n" +
        "            if (d is {1} o)\r\n" +
        //"            {{\r\n" +
        "                o.On{0}Changed({4}, {5});\r\n" +
        // "            }}\r\n" +
        "        }}));\r\n\r\n" +
        "    partial void On{0}Changed({2} o, {2} n);\r\n";

    string TEMPLATE2 =
        "    public {2} {0}\r\n" +
        "    {{\r\n" +
        "        get {{ return ({2})GetValue({0}Property); }}\r\n" +
        "        {7} set {{ SetValue({0}Property, value); }}\r\n" +
        "    }}\r\n\r\n" +
        "    public static readonly DependencyProperty {0}Property =\r\n" +
        "        DependencyProperty.Register(nameof({0}), typeof({2}), typeof({1}), new PropertyMetadata({3}, (d, e) =>\r\n" +
        "        {{\r\n" +
        "            if (d is {1} o)\r\n" +
        "                o.{6};\r\n" +
        "        }}));\r\n\r\n";

    const string PROPERTY_CHANGED_CACHE_SOURCE =
        "using System.Collections.Generic;\r\n" +
        "using System.ComponentModel;\r\n\r\n" +
        "namespace CharacterMap.Core;\r\n\r\n" +
        "public static class PropertyChangedCache\r\n" +
        "{\r\n" +
        "    static Dictionary<string, PropertyChangedEventArgs> _cache = [];\r\n\r\n" +
        "    public static PropertyChangedEventArgs Get(string name)\r\n" +
        "    {\r\n" +
        "        if (!_cache.TryGetValue(name, out PropertyChangedEventArgs value))\r\n" +
        "            _cache[name] = value = new(name);\r\n\r\n" +
        "        return value;\r\n" +
        "    }\r\n" +
        "}\r\n";

    static bool IsDependencyPropertyAttribute(AttributeSyntax a)
    {
        string name = a.Name switch
        {
            GenericNameSyntax g => g.Identifier.ValueText,
            IdentifierNameSyntax i => i.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right is GenericNameSyntax qg ? qg.Identifier.ValueText : q.Right.Identifier.ValueText,
            _ => a.Name.ToString()
        };
        return name is "DependencyProperty" or "DependencyPropertyAttribute";
    }

    static bool HasOnPropertyChangedMethod(INamedTypeSymbol classSymbol, string parameterFullTypeName)
    {
        INamedTypeSymbol current = classSymbol;
        while (current is not null)
        {
            foreach (IMethodSymbol method in current.GetMembers("OnPropertyChanged").OfType<IMethodSymbol>())
            {
                if (method.Parameters.Length == 1
                    && method.Parameters[0].Type.ToDisplayString() == parameterFullTypeName
                    && (SymbolEqualityComparer.Default.Equals(current, classSymbol)
                        || method.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal or Accessibility.Internal))
                    return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("PropertyChangedCache.g.cs", SourceText.From(PROPERTY_CHANGED_CACHE_SOURCE, Encoding.UTF8)));

        var classNodes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => ctx.Node as ClassDeclarationSyntax)
            .Where(static c => c is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(classNodes.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
        {
            var (compilation, classList) = source;
            List<DPData> data = [];

            foreach (ClassDeclarationSyntax n in classList.OfType<ClassDeclarationSyntax>()
                .Where(c => c.AttributeLists.SelectMany(l => l.Attributes).Any(IsDependencyPropertyAttribute)))
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(n.SyntaxTree);
                INamedTypeSymbol classSymbol = semanticModel.GetDeclaredSymbol(n) as INamedTypeSymbol;
                bool notifyPropertyChanged = n.HasAttribute("DependencyPropertyChanged");
                bool implementsINPC = notifyPropertyChanged && classSymbol?.AllInterfaces.Any(
                    i => i.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged") == true;
                bool hasArgsOnPropertyChanged = notifyPropertyChanged && classSymbol is not null
                    && HasOnPropertyChangedMethod(classSymbol, "System.ComponentModel.PropertyChangedEventArgs");
                bool hasStringOnPropertyChanged = notifyPropertyChanged && classSymbol is not null
                    && HasOnPropertyChangedMethod(classSymbol, "string");
                bool isSealed = classSymbol?.IsSealed == true;

                DPData src = new()
                {
                    ParentClass = n.Identifier.ValueText,
                    ParentNamespace = n.GetNamespace(),
                    // Does not support static usings or using alias'
                    Usings = (n.Parent?.Parent as CompilationUnitSyntax)?.Usings.Select(u => $"using {u.Name.ToString()};")?.ToList() ?? [],
                    NotifyPropertyChanged = notifyPropertyChanged,
                    ImplementsINPC = implementsINPC,
                    HasArgsOnPropertyChanged = hasArgsOnPropertyChanged,
                    HasStringOnPropertyChanged = hasStringOnPropertyChanged,
                    IsSealed = isSealed
                };

                foreach (AttributeSyntax a in n.AttributeLists
                    .SelectMany(s => s.Attributes)
                    .Where(IsDependencyPropertyAttribute))
                {
                    string type = null;
                    ITypeSymbol typeSymbol = null;
                    if (a.Name is GenericNameSyntax gen)
                    {
                        TypeSyntax typeSyntax = gen.TypeArgumentList.Arguments[0];
                        type = typeSyntax.ToString();
                        typeSymbol = semanticModel.GetTypeInfo(typeSyntax).Type;
                    }
                    else
                    {
                        AttributeArgumentSyntax typeArg = a.GetArgument("Type");
                        if (typeArg != null && typeArg.Expression is TypeOfExpressionSyntax typeofSyntax)
                        {
                            typeSymbol = semanticModel.GetTypeInfo(typeofSyntax.Type).Type;
                            type = typeofSyntax.Type.ToString();
                        }
                    }

                    bool isValueType = false;
                    if (typeSymbol != null)
                    {
                        if (typeSymbol.TypeKind == TypeKind.Error)
                            isValueType = DPData.IsPrimitiveFallback(type);
                        else
                            isValueType = typeSymbol.IsValueType;
                    }
                    else
                        isValueType = DPData.IsPrimitiveFallback(type ?? "object");

                    bool isReadOnly = a.GetArgument("IsReadOnly")?.GetValue() == "true";

                    List<AttributeArgumentSyntax> positionalArgs = a.ArgumentList?.Arguments
                        .Where(arg => arg.NameColon == null && arg.NameEquals == null).ToList() ?? [];

                    DPData d = a.GetArgument("Name") is { } na && na.NameEquals is { } ne // Attribute property path,
                        ? src with
                        {
                            Name = a.GetArgument("Name")?.GetValue(),
                            Default = a.GetArgument("Default")?.GetValue() ?? "default",
                            Type = type ?? a.GetArgument("Type")?.GetValue()?.Replace("typeof(", string.Empty).Replace(")", string.Empty) ?? "object",
                            IsValueType = isValueType,
                            IsReadOnly = isReadOnly
                        }
                        : src with // Constructor path - preferred
                        {
                            Name = positionalArgs.Count > 0 ? positionalArgs[0].GetValue() : type,
                            Type = type ?? "object",
                            Default = positionalArgs.Count > 1 ? positionalArgs[1].GetValue() : "default",
                            Callback = FormatCallback(positionalArgs.Count > 2 ? positionalArgs[2].GetValue() : null),
                            IsValueType = isValueType,
                            IsReadOnly = isReadOnly
                        };

                    static string FormatCallback(string input)
                    {
                        if (input != null && input.StartsWith("nameof("))
                            input = input.Remove(0, "nameof(".Length)[0..^1];
                        return input;
                    }

                    data.Add(d);
                }
            }

            if (data.Count == 0)
                return;

            foreach (IGrouping<string, DPData> group in data.GroupBy(d => $"{d.ParentNamespace}.{d.ParentClass}.g.dp.cs"))
            {
                string file = group.Key;
                DPData first = group.First();
                string ns = first.ParentNamespace;
                string target = first.ParentClass;
                List<string> usings = [.. first.Usings];

                bool notify = group.Any(d => d.NotifyPropertyChanged);
                bool implementsINPC = group.Any(d => d.ImplementsINPC);
                bool hasArgs = group.Any(d => d.HasArgsOnPropertyChanged);
                bool hasString = group.Any(d => d.HasStringOnPropertyChanged);
                bool isSealed = first.IsSealed;

                if (usings.Contains("using Windows.UI.Xaml;") is false)
                    usings.Add("using Windows.UI.Xaml;");

                if (notify)
                {
                    if (usings.Contains("using System.ComponentModel;") is false)
                        usings.Add("using System.ComponentModel;");
                    if (usings.Contains("using CharacterMap.Core;") is false)
                        usings.Add("using CharacterMap.Core;");
                }

                StringBuilder sb = new();

                if (notify && !implementsINPC)
                {
                    string access = isSealed ? "private" : "protected";
                    sb.AppendLine("    public event PropertyChangedEventHandler PropertyChanged;");
                    sb.AppendLine();
                    if (!hasString)
                    {
                        sb.AppendLine($"    {access} void OnPropertyChanged(string name)");
                        sb.AppendLine("    {");
                        sb.AppendLine("        PropertyChanged?.Invoke(this, PropertyChangedCache.Get(name));");
                        sb.AppendLine("    }");
                        sb.AppendLine();
                    }
                    if (!hasArgs)
                    {
                        sb.AppendLine($"    {access} void OnPropertyChanged(PropertyChangedEventArgs args)");
                        sb.AppendLine("    {");
                        sb.AppendLine("        PropertyChanged?.Invoke(this, args);");
                        sb.AppendLine("    }");
                        sb.AppendLine();
                    }
                }

                bool callWithArgs = !implementsINPC || hasArgs || !hasString;

                foreach (DPData dp in group)
                {
                    string propChangedCall = notify
                        ? (callWithArgs
                            ? $"o.OnPropertyChanged(PropertyChangedCache.Get(nameof({dp.Name})));"
                            : $"o.OnPropertyChanged(nameof({dp.Name}));")
                        : null;

                    if (notify)
                    {
                        string action = string.IsNullOrWhiteSpace(dp.Callback)
                            ? $"o.On{dp.Name}Changed({dp.GetCastType("e.OldValue")}, {dp.GetCastType("e.NewValue")});"
                            : $"o.{dp.GetCallback()};";

                        sb.AppendLine($"    public {dp.Type} {dp.Name}");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        get {{ return ({dp.Type})GetValue({dp.Name}Property); }}");
                        sb.AppendLine($"        {(dp.IsReadOnly ? "private" : string.Empty)} set {{ SetValue({dp.Name}Property, value); }}");
                        sb.AppendLine("    }");
                        sb.AppendLine();
                        sb.AppendLine($"    public static readonly DependencyProperty {dp.Name}Property =");
                        sb.AppendLine($"        DependencyProperty.Register(nameof({dp.Name}), typeof({dp.Type}), typeof({dp.ParentClass}), new PropertyMetadata({dp.GetDefault()}, (d, e) =>");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            if (d is {dp.ParentClass} o)");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                {action}");
                        sb.AppendLine($"                {propChangedCall}");
                        sb.AppendLine("            }");
                        sb.AppendLine("        }));");
                        sb.AppendLine();
                        if (string.IsNullOrWhiteSpace(dp.Callback))
                        {
                            sb.AppendLine($"    partial void On{dp.Name}Changed({dp.Type} o, {dp.Type} n);");
                            sb.AppendLine();
                        }
                    }
                    else
                    {
                        sb.AppendLine(
                            string.Format(
                                string.IsNullOrWhiteSpace(dp.Callback) ? TEMPLATE : TEMPLATE2,
                                dp.Name,
                                dp.ParentClass,
                                dp.Type,
                                dp.GetDefault(),
                                dp.GetCastType("e.OldValue"),
                                dp.GetCastType("e.NewValue"),
                                dp.GetCallback(),
                                dp.IsReadOnly ? "private" : string.Empty));
                    }
                }

                string baseList = notify && !implementsINPC ? " : INotifyPropertyChanged" : string.Empty;

                spc.AddSource(file, SourceText.From(
$@"{string.Join("\r\n", usings)}

namespace {ns};

partial class {target}{baseList}
{{
{sb}
}}", Encoding.UTF8));
            }
        });
    }
}
