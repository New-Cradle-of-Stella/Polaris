using System;
using Polaris.Event.Compiler.Diagnostics;
using Polaris.Event.Compiler.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Polaris.Event.Compiler.Aliases
{
    public static class AliasLoader
    {
        public static AliasDocument Load(SourceText source, DiagnosticBag diagnostics)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            AliasDocument document;
            try
            {
                document = deserializer.Deserialize<AliasDocument>(source.Content) ?? new AliasDocument();
            }
            catch (Exception ex)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.AliasParseFailure,
                    DiagnosticSeverity.Error,
                    $"Failed to parse alias file: {ex.Message}",
                    new SourceSpan(source.Path, 1, 1)));
                return new AliasDocument();
            }

            Validate(document, source.Path, diagnostics);
            return document;
        }

        static void Validate(AliasDocument document, string path, DiagnosticBag diagnostics)
        {
            // 姿势只能挂在已声明角色下——由 Dictionary<角色, ActorAlias{Poses}> 的嵌套结构天然保证，
            // 这里只再检查一层最基本的健壮性：角色必须提供 raw 映射，否则后面所有引用它的 @char/对话行
            // 都会在 Binding 阶段查到一个没有 raw 的“空”角色。
            foreach (var kv in document.Actors)
            {
                if (kv.Value == null || string.IsNullOrWhiteSpace(kv.Value.Raw))
                {
                    diagnostics.Report(new HppDiagnostic(
                        DiagnosticCodes.UnknownActor,
                        DiagnosticSeverity.Error,
                        $"Actor alias '{kv.Key}' is missing a 'raw' mapping.",
                        new SourceSpan(path, 1, 1)));
                }
            }
        }
    }
}
