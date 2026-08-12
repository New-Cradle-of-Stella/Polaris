using System.Collections.Generic;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler.Syntax
{
    public static class HxxLexer
    {
        public static IReadOnlyList<HxxRawLine> Tokenize(SourceText source)
        {
            var result = new List<HxxRawLine>(source.Lines.Count);
            for (int i = 0; i < source.Lines.Count; i++)
            {
                string line = source.Lines[i];
                int indent = 0;
                while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
                {
                    indent++;
                }

                string content = line.Substring(indent).TrimEnd();
                result.Add(new HxxRawLine(i + 1, indent, content));
            }

            return result;
        }
    }
}
