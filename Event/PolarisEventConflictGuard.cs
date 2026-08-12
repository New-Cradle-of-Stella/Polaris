using System.Collections.Generic;
using System.Reflection;
using Polaris.Diagnostics;

namespace Polaris.Event
{
    /// <summary>
    /// 事件 ID 冲突的收集与处置，结构照抄 <c>Lang\PlangConflictGuard.cs</c>：扫描期间收集，
    /// 扫描结束时（<see cref="Seal"/>）把所有冲突汇总成一条致命错误上报；扫描结束之后才出现的冲突
    /// （运行期直接调 <see cref="PolarisEventRegistrationContext.Register"/>）当场单独报一条。
    /// </summary>
    internal static class PolarisEventConflictGuard
    {
        internal sealed class Conflict
        {
            public string RuntimeKey { get; }
            public Assembly Kept { get; }
            public Assembly Ignored { get; }

            public Conflict(string runtimeKey, Assembly kept, Assembly ignored)
            {
                RuntimeKey = runtimeKey;
                Kept = kept;
                Ignored = ignored;
            }

            public string Describe() => $"{RuntimeKey} -- kept({Kept?.GetName().Name}) <-> ignored({Ignored?.GetName().Name})";
        }

        static readonly List<Conflict> conflicts = new List<Conflict>();
        static bool scanFinished;

        internal static void Record(string runtimeKey, Assembly kept, Assembly ignored)
        {
            var conflict = new Conflict(runtimeKey, kept, ignored);
            conflicts.Add(conflict);

            Plugin.Logger.LogError($"[PolarisEvent] event id conflict: {conflict.Describe()}");

            if (scanFinished)
            {
                RaiseFatal(new[] { conflict });
            }
        }

        internal static void Seal()
        {
            scanFinished = true;

            if (conflicts.Count > 0)
            {
                RaiseFatal(conflicts);
            }
        }

        static void RaiseFatal(IReadOnlyList<Conflict> batch)
        {
            var fatal = new FatalError(MyPluginInfo.PLUGIN_NAME, Reason)
            {
                Action = Action,
            };

            foreach (var conflict in batch)
            {
                fatal.Details.Add(conflict.Describe());
                AddCulprit(fatal, conflict.Kept);
                AddCulprit(fatal, conflict.Ignored);
            }

            PolarisAPI.Errors.Fatal(fatal);
        }

        static void AddCulprit(FatalError fatal, Assembly assembly)
        {
            if (assembly != null && !fatal.Culprits.Contains(assembly))
            {
                fatal.Culprits.Add(assembly);
            }
        }

        static readonly FatalText Reason = new FatalText(
            english:
                "Two or more mods registered a PolarisEvent with the same namespace and logical id. Which one runs "
                + "depends on mod load order, so the wrong story/event content could play.",
            chinese:
                "有两个以上的模组注册了相同命名空间和逻辑 ID 的 PolarisEvent。哪一个生效取决于模组加载顺序，"
                + "游戏里可能会播放到错误的剧情/事件内容。",
            japanese:
                "同一のネームスペースとロジカルIDを持つPolarisEventが複数のMODから登録されました。どちらが有効になるかは"
                + "MODの読み込み順に依存するため、誤ったストーリー/イベント内容が再生される可能性があります。");

        static readonly FatalText Action = new FatalText(
            english:
                "· Until it is fixed, keep only one of the mods listed above enabled (Polaris page on the title screen).\n"
                + "· Send this report to their authors: one side has to change its project's default namespace and "
                + "regenerate its .phxx files.",
            chinese:
                "· 在修好之前，上面列出的模组只保留一个（在标题画面的 Polaris 页里关掉其余的）。\n"
                + "· 请把这份报告交给它们的作者：必须有一方修改自己项目的默认命名空间（Default Namespace）并"
                + "重新生成 .phxx 文件。",
            japanese:
                "· 修正されるまでは、上記のMODのうち一つだけを有効にしてください（タイトル画面の Polaris ページ）。\n"
                + "· このレポートを各作者へご提出ください：どちらか一方がプロジェクトの既定の名前空間"
                + "（Default Namespace）を変更し、.phxx ファイルを再生成する必要があります。");
    }
}
