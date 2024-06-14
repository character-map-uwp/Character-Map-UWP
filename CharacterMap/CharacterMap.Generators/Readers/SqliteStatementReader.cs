using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterMap.Generators.Readers;

internal record class SQLSTData
{
    public string Name { get; init; }
    public string Type { get; init; }
    public bool Single { get; init; }
    public IReadOnlyList<(string CastType, string PropertyName, string ReadType, int index)> Columns { get; init; }

}
public class SqliteStatementReader : SyntaxReader
{
    List<SQLSTData> data = [];

    public override void Read(IEnumerable<SyntaxNode> nodes)
    {
        static string GetName(string s)
        {
            var i = s.IndexOf("(") + 1;
            if (i > 0)
            {
                s = s.Remove(0, i);
                s = s.Remove(s.LastIndexOf(")"));
            }

            if (s.Length > 2 && s.LastIndexOf(".") is int d && d > 0)
                s = s.Remove(0, d + 1);

            return s;
        }

        foreach (var n in nodes.OfType<ClassDeclarationSyntax>()
                               .Where(c => c.HasGenericAttribute("SQLReader")))
        {
            string type = null; 
            string name = null;
            bool single = false;

            // 1. Get the class type
            foreach (var a in n.AttributeLists
                               .SelectMany(s => s.Attributes)
                               .Where(a => a.Name.ToString().StartsWith("SQLReader") && !a.Name.ToString().StartsWith("SQLReaderMapping")))
            {
                type = ((GenericNameSyntax)a.Name).TypeArgumentList.Arguments[0].ToString();
                name = GetName(a.ArgumentList.Arguments[0].GetValue());

                if (a.ArgumentList.Arguments.Skip(1).FirstOrDefault() is AttributeArgumentSyntax v
                    && v.GetValue() == "true")
                    single = true;
            }

            List<(string, string, string, int)> columns = [];

            // 2. Get the column mappings
            foreach (var a in n.AttributeLists
                               .SelectMany(s => s.Attributes)
                               .Where(a => a.Name.ToString().StartsWith("SQLReaderMapping")))
            {
                var mappingtype = ((GenericNameSyntax)a.Name).TypeArgumentList.Arguments[0].ToString();
                var mappingpro = GetName(a.ArgumentList.Arguments[0].GetValue());
                string t = mappingtype;
                int i = -1;

                if (a.ArgumentList.Arguments.Skip(1).FirstOrDefault() is AttributeArgumentSyntax v)
                    t = GetName(v.GetValue());

                if (a.ArgumentList.Arguments.Skip(2).FirstOrDefault() is AttributeArgumentSyntax v2)
                    i = Convert.ToInt32(v2.GetValue());

                columns.Add((mappingtype, mappingpro, t, i));
            }

            data.Add(new() { Name = name, Type = type, Single = single, Columns = columns });
        }
    }

    string TEMPLATE_0 =
        "using SQLite;\r\n" +
        "\r\n" +
        "namespace CharacterMap.Services;\r\n" +
        "\r\n" +
        "public static partial class SQLite3Readers\r\n" +
        "{{\r\n" +
        "{0}\r\n" +
        "}}";

    //string TEMPLATE =
    //    "    public static List<{0}> ReadAs{1}s(this SQLitePCL.sqlite3_stmt stmt)\r\n" +
    //    "    {{\r\n" +
    //    "        List<{0}> data = new();\r\n" +
    //    "        while (SQLite3.Step(stmt) == SQLite3.Result.Row)\r\n" +
    //    "        {{\r\n" +
    //    "            data.Add(\r\n" +
    //    "                new {0}()\r\n" +
    //    "                {{\r\n" +
    //    "{2}\r\n" +
    //    "                }});\r\n" +
    //    "        }}\r\n\r\n" +
    //    "        return data;\r\n" +
    //    "    }}\r\n";

    string TEMPLATE =
        "    public static List<{0}> ReadAs{1}s(this SQLiteCommand cmd)\r\n" +
        "    {{\r\n" +
        "        var stmt = cmd.Prepare();\r\n" +
        "\r\n" +
        "        try\r\n" +
        "        {{\r\n" +
        "            List<{0}> data = new();\r\n" +
        "            while (SQLite3.Step(stmt) == SQLite3.Result.Row)\r\n" +
        "            {{\r\n" +
        "                data.Add(\r\n" +
        "                    new {0}()\r\n" +
        "                    {{\r\n" +
        "{2}\r\n" +
        "                    }});\r\n" +
        "            }}\r\n" +
        "            return data;\r\n" +
        "        }}\r\n" +
        "        finally\r\n" +
        "        {{\r\n" +
        "            stmt.Dispose();\r\n" +
        "        }}\r\n" +
        "    }}\r\n";

    string TEMPLATE_S =
        "    public static {0} ReadAs{1}(this SQLiteCommand cmd)\r\n" +
        "    {{\r\n" +
        "        var stmt = cmd.Prepare();\r\n" +
        "        try\r\n" +
        "        {{\r\n" +
        "            while (SQLite3.Step(stmt) == SQLite3.Result.Row)\r\n" +
        "                return new {0}()\r\n" +
        "                    {{\r\n" +
        "{2}\r\n" +
        "                    }};\r\n" +
        "        }}\r\n" +
        "        finally\r\n" +
        "        {{\r\n" +
        "            stmt.Dispose();\r\n" +
        "        }}\r\n" +
        "        return default;\r\n" +
        "    }}";

    public override void Write(GeneratorExecutionContext context)
    {
        base.Write(context);

        if (data.Count == 0)
            return;

        StringBuilder main = new ();
        StringBuilder sb = new ();

        foreach (var item in data)
        {
            sb.Clear();

            int i = 0;
            foreach (var d in item.Columns)
            {
                int idx = d.index >= 0 ? d.index : i;
                var line = $"{d.PropertyName} = ({d.CastType})SQLite3.Column{GetReader(d.ReadType)}(stmt, {idx}),";
                sb.AppendLine(6, line);
                i++;
            }

            main.AppendLine(
                string.Format(
                    item.Single ? TEMPLATE_S : TEMPLATE,
                    item.Type, 
                    item.Name, 
                    sb.ToString().TrimEnd().TrimEnd(',')));
        }

        static string GetReader(string type)
        {
            return $"{char.ToUpper(type[0])}{type[1..^0]}";
        }


        context.AddSource("SQLite3Readers.g.cs", 
            SourceText.From(string.Format(TEMPLATE_0, main.ToString().TrimEnd()), Encoding.UTF8));
    }
}
