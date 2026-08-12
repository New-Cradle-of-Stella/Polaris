using Polaris.Event.Compiler.Aliases;
using Polaris.Event.Compiler.Diagnostics;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler.Binding
{
    /// <summary>
    /// 把脚本里出现的可读别名（角色、姿势、位置、音效、事件）解析成底层原始值，解析失败时报出带
    /// 拼写建议的诊断。别名查找目前一律大小写敏感（见 <see cref="AliasSettings.CaseInsensitive"/> 的说明）。
    /// </summary>
    public sealed class AliasBinder
    {
        readonly DiagnosticBag diagnostics;

        public AliasBinder(AliasDocument aliases, DiagnosticBag diagnostics)
        {
            Aliases = aliases;
            this.diagnostics = diagnostics;
        }

        public AliasDocument Aliases { get; }

        public bool TryResolveActor(string name, SourceSpan span, out ActorAlias actor)
        {
            if (Aliases.Actors.TryGetValue(name, out actor))
            {
                return true;
            }

            diagnostics.Report(new HppDiagnostic(
                DiagnosticCodes.UnknownActor,
                DiagnosticSeverity.Error,
                $"Unknown actor '{name}'.",
                span,
                Suggest(Aliases.Actors.Keys, name, s => $"Did you mean: {s}?")));
            actor = null;
            return false;
        }

        public bool TryResolvePose(ActorAlias actor, string actorName, string poseName, SourceSpan span, out string rawPose)
        {
            if (actor.Poses.TryGetValue(poseName, out rawPose))
            {
                return true;
            }

            diagnostics.Report(new HppDiagnostic(
                DiagnosticCodes.UnknownPose,
                DiagnosticSeverity.Error,
                $"Actor '{actorName}' has no pose '{poseName}'.",
                span,
                Suggest(actor.Poses.Keys, poseName, s => $"Did you mean: {actorName}.{s}?")));
            rawPose = null;
            return false;
        }

        public bool TryResolvePosition(string name, SourceSpan span, out PositionAlias position)
        {
            if (Aliases.Positions.TryGetValue(name, out position))
            {
                return true;
            }

            diagnostics.Report(new HppDiagnostic(
                DiagnosticCodes.UnknownPosition,
                DiagnosticSeverity.Error,
                $"Unknown position '{name}'.",
                span,
                Suggest(Aliases.Positions.Keys, name, s => $"Did you mean: {s}?")));
            position = null;
            return false;
        }

        public bool TryResolveSfx(string name, SourceSpan span, out string raw)
        {
            if (Aliases.Audio.Sfx.TryGetValue(name, out raw))
            {
                return true;
            }

            diagnostics.Report(new HppDiagnostic(
                DiagnosticCodes.UnknownSfxAlias,
                DiagnosticSeverity.Error,
                $"Unknown sound effect alias '{name}'.",
                span,
                Suggest(Aliases.Audio.Sfx.Keys, name, s => $"Did you mean: {s}?")));
            raw = null;
            return false;
        }

        public bool TryResolveEvent(string name, SourceSpan span, out string raw)
        {
            if (Aliases.Events.TryGetValue(name, out raw))
            {
                return true;
            }

            diagnostics.Report(new HppDiagnostic(
                DiagnosticCodes.UnknownEventAlias,
                DiagnosticSeverity.Error,
                $"Unknown event alias '{name}'.",
                span,
                Suggest(Aliases.Events.Keys, name, s => $"Did you mean: {s}?")));
            raw = null;
            return false;
        }

        static string Suggest(System.Collections.Generic.IEnumerable<string> candidates, string input, System.Func<string, string> format)
        {
            string suggestion = SpellingSuggestions.Suggest(candidates, input);
            return suggestion == null ? null : format(suggestion);
        }
    }
}
