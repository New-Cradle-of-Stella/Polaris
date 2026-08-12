using System.Collections.Generic;
using Polaris.Event.Compiler.Diagnostics;

namespace Polaris.Event.Compiler
{
    /// <summary>逐文件诊断，供编辑器实时诊断使用（本轮不实现编辑器，只保留 API 形状供阶段4接入）。</summary>
    public sealed class HppAnalysisResult
    {
        public IReadOnlyDictionary<string, IReadOnlyList<HppDiagnostic>> DiagnosticsByFile { get; }

        public HppAnalysisResult(IReadOnlyDictionary<string, IReadOnlyList<HppDiagnostic>> diagnosticsByFile)
        {
            DiagnosticsByFile = diagnosticsByFile;
        }
    }
}
