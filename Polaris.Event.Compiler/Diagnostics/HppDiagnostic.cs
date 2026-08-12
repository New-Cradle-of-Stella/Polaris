using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler.Diagnostics
{
    public sealed class HppDiagnostic
    {
        public string Code { get; }
        public DiagnosticSeverity Severity { get; }
        public string Message { get; }
        public SourceSpan Span { get; }

        /// <summary>拼写建议或修复提示，面向作者，如"Did you mean: Noel.Happy?"。没有时为 null。</summary>
        public string Suggestion { get; }

        public HppDiagnostic(string code, DiagnosticSeverity severity, string message, SourceSpan span, string suggestion = null)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Span = span;
            Suggestion = suggestion;
        }

        public override string ToString()
            => Suggestion == null ? $"{Span} {Code} {Message}" : $"{Span} {Code} {Message} {Suggestion}";
    }
}
