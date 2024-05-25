using CharacterMap.Generators.Readers;
using Microsoft.CodeAnalysis;
using System;

namespace CharacterMap.Generators;

[Generator]
public class SourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context)
    {
        //System.Diagnostics.Debugger.Launch();

        try
        {
            foreach (SyntaxTree tree in context.Compilation.SyntaxTrees)
            {
                // 1. Handle Unicode Ranges
                new UnicodeFilterReader().Process(tree, context);

                // 2. Handle FileNameWriter
                new FileNameReader().Process(tree, context);

                // 3. Create Dependency Properties
                new DependencyPropertyReader().Process(tree, context);

                // 4. Create Attached Properties
                new AttachedPropertyReader().Process(tree, context);

                // 5. Generate SQLite Readers
                new SqliteStatementReader().Process(tree, context);
            }
        }
        catch (Exception)
        {

        }
    }
}