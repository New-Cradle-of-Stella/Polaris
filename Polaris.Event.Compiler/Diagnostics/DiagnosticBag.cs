using System.Collections.Generic;

namespace Polaris.Event.Compiler.Diagnostics
{
    public sealed class DiagnosticBag
    {
        readonly List<HppDiagnostic> diagnostics = new List<HppDiagnostic>();

        public IReadOnlyList<HppDiagnostic> Diagnostics => diagnostics;

        public bool HasErrors
        {
            get
            {
                foreach (var d in diagnostics)
                {
                    if (d.Severity == DiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Report(HppDiagnostic diagnostic) => diagnostics.Add(diagnostic);
    }
}
