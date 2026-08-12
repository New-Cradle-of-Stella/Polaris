using System.Collections.Generic;
using Polaris.Event.Compiler.Diagnostics;
using Polaris.Event.Compiler.Parsing;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler
{
    public sealed class HppParseResult
    {
        public SourceText Source { get; }
        public IReadOnlyList<HxxNode> Nodes { get; }
        public IReadOnlyList<HppDiagnostic> Diagnostics { get; }

        public HppParseResult(SourceText source, IReadOnlyList<HxxNode> nodes, IReadOnlyList<HppDiagnostic> diagnostics)
        {
            Source = source;
            Nodes = nodes;
            Diagnostics = diagnostics;
        }
    }
}
