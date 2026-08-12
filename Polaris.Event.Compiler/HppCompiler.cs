using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Polaris.Event.Compiler.Aliases;
using Polaris.Event.Compiler.Binding;
using Polaris.Event.Compiler.Diagnostics;
using Polaris.Event.Compiler.Emission;
using Polaris.Event.Compiler.Lowering;
using Polaris.Event.Compiler.Parsing;
using Polaris.Event.Compiler.Syntax;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler
{
    /// <summary>
    /// 编译器唯一的公开入口。三条调用路径（编辑器实时诊断、MSBuild 构建、单元测试/未来的 CLI）
    /// 都必须走这三个方法，不允许任何一条路径另外维护一份 parser/binder/lowerer，
    /// 否则编辑器诊断和构建诊断迟早会漂移（PolarisEvent-实现计划.md §10 的第一条风险）。
    /// </summary>
    public sealed class HppCompiler
    {
        public HppParseResult Parse(SourceText source)
        {
            var diagnostics = new DiagnosticBag();
            var lines = HxxLexer.Tokenize(source);
            var parser = new HxxParser(lines, source.Path, diagnostics);
            var nodes = parser.ParseDocument();
            return new HppParseResult(source, nodes, diagnostics.Diagnostics);
        }

        public HppAnalysisResult Analyze(HppProject project, CancellationToken token)
        {
            var result = new Dictionary<string, IReadOnlyList<HppDiagnostic>>();

            var aliasDiagnostics = new DiagnosticBag();
            var aliases = LoadAliases(project, aliasDiagnostics);
            if (project.AliasFile != null)
            {
                result[project.AliasFile.Path] = aliasDiagnostics.Diagnostics;
            }

            foreach (var file in project.Files)
            {
                token.ThrowIfCancellationRequested();

                var parseResult = Parse(file);
                var fileDiagnostics = new DiagnosticBag();
                foreach (var d in parseResult.Diagnostics)
                {
                    fileDiagnostics.Report(d);
                }

                var binder = new AliasBinder(aliases, fileDiagnostics);
                new HxxLowerer(binder, fileDiagnostics, project.StrictRaw, file.Path).Lower(parseResult.Nodes);

                result[file.Path] = fileDiagnostics.Diagnostics;
            }

            return new HppAnalysisResult(result);
        }

        public HppCompileResult Compile(HppProject project, CancellationToken token)
        {
            var allDiagnostics = new List<HppDiagnostic>();

            var aliasDiagnostics = new DiagnosticBag();
            var aliases = LoadAliases(project, aliasDiagnostics);
            allDiagnostics.AddRange(aliasDiagnostics.Diagnostics);

            var compiledFiles = new List<HppCompiledFile>();

            foreach (var file in project.Files)
            {
                token.ThrowIfCancellationRequested();

                var parseResult = Parse(file);
                var fileDiagnostics = new DiagnosticBag();
                foreach (var d in parseResult.Diagnostics)
                {
                    fileDiagnostics.Report(d);
                }

                var binder = new AliasBinder(aliases, fileDiagnostics);
                var loweredLines = new HxxLowerer(binder, fileDiagnostics, project.StrictRaw, file.Path).Lower(parseResult.Nodes);

                allDiagnostics.AddRange(fileDiagnostics.Diagnostics);

                if (fileDiagnostics.HasErrors)
                {
                    continue; // 有错误的文件不产出代码，但不影响其它文件继续编译
                }

                string logicalId = DeriveLogicalId(file.Path);
                string commandText = CmdEmitter.Emit(loweredLines);
                string contentHash = ComputeHash(commandText);
                string generatedCSharp = CSharpEmitter.Emit(
                    project.Namespace, project.RootNamespace, logicalId, file.Path, commandText, contentHash);
                string sourceMapJson = HmapWriter.Write(loweredLines);

                compiledFiles.Add(new HppCompiledFile(file.Path, logicalId, commandText, generatedCSharp, sourceMapJson, contentHash));
            }

            bool success = !allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
            return new HppCompileResult(compiledFiles, allDiagnostics, success);
        }

        static AliasDocument LoadAliases(HppProject project, DiagnosticBag diagnostics)
            => project.AliasFile != null ? AliasLoader.Load(project.AliasFile, diagnostics) : new AliasDocument();

        static string DeriveLogicalId(string path)
        {
            string normalized = path.Replace('\\', '/').TrimStart('/');
            return normalized.EndsWith(".phxx") ? normalized.Substring(0, normalized.Length - 5) : normalized;
        }

        static string ComputeHash(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
