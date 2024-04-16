using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace CharacterMap.Generators;

public abstract class SyntaxReader
{
    public void Process(SyntaxTree tree, GeneratorExecutionContext context)
    {
        Read(tree);
        Write(context);
    }

    public void Read(SyntaxTree tree) => Read(tree.GetRoot().DescendantNodesAndSelf());

    public virtual void Read(IEnumerable<SyntaxNode> nodes) { }

    public virtual void Write(GeneratorExecutionContext context) { }
}
