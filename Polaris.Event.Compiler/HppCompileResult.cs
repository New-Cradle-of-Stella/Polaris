using System.Collections.Generic;
using Polaris.Event.Compiler.Diagnostics;

namespace Polaris.Event.Compiler
{
    public sealed class HppCompiledFile
    {
        public string SourcePath { get; }
        public string LogicalId { get; }
        public string CommandText { get; }
        public string GeneratedCSharp { get; }
        public string SourceMapJson { get; }
        public string ContentHash { get; }

        public HppCompiledFile(
            string sourcePath,
            string logicalId,
            string commandText,
            string generatedCSharp,
            string sourceMapJson,
            string contentHash)
        {
            SourcePath = sourcePath;
            LogicalId = logicalId;
            CommandText = commandText;
            GeneratedCSharp = generatedCSharp;
            SourceMapJson = sourceMapJson;
            ContentHash = contentHash;
        }
    }

    public sealed class HppCompileResult
    {
        public IReadOnlyList<HppCompiledFile> Files { get; }
        public IReadOnlyList<HppDiagnostic> Diagnostics { get; }
        public bool Success { get; }

        public HppCompileResult(IReadOnlyList<HppCompiledFile> files, IReadOnlyList<HppDiagnostic> diagnostics, bool success)
        {
            Files = files;
            Diagnostics = diagnostics;
            Success = success;
        }
    }
}
