using System.Collections.Generic;
using System.Reflection;
using Polaris.Diagnostics;

namespace Polaris.Lang
{
    /// <summary>
    /// key 冲突的收集与处置：一旦有 key 被两个模组重复注册就致命报错、拦在标题画面，因为哪份文案生效取决于
    /// 加载顺序，会在界面上串出另一个模组的文字且几乎无法从表象追查。扫描期间收集，结束时汇总成一条错误（<see cref="Seal"/>）；
    /// 扫描结束后出现的冲突当场单独上报。
    /// </summary>
    internal static class PlangConflictGuard
    {
        static readonly List<PlangConflict> conflicts = new();

        /// <summary>扫描已经结束过一次，此后的冲突当场上报。</summary>
        static bool scanFinished;

        /// <summary>
        /// 当前正在执行注册的那个生成类所属的程序集，由 <see cref="PlangRegistryScanner"/> 在调用
        /// <see cref="IPlangRegistrar.Register"/> 前后设置/清空。用这种环境变量式的传递而不加方法参数，
        /// 是为了不破坏 PolarisTools 生成代码的既有签名。
        /// </summary>
        internal static Assembly CurrentSource { get; set; }

        /// <summary>记一次冲突；<paramref name="kept"/> 是先注册、文案被保留的一方，保证同一次启动内结果稳定。</summary>
        internal static void Record(string key, Assembly kept, Assembly ignored)
        {
            var conflict = new PlangConflict(key, kept, ignored);
            conflicts.Add(conflict);

            // 用 LogError 而非 LogFatal：LogFatal 会被日志监听器再建一条重复错误档，权威记录是下面的 Errors.Fatal。
            Plugin.Logger.LogError($"[PolarisLang] key conflict: {conflict.Describe()}");

            if (scanFinished)
            {
                RaiseFatal(new[] { conflict });
            }
        }

        /// <summary>扫描结束时调用一次：有冲突就汇总成一条致命错误上报。</summary>
        internal static void Seal()
        {
            scanFinished = true;

            if (conflicts.Count > 0)
            {
                RaiseFatal(conflicts);
            }
        }

        static void RaiseFatal(IReadOnlyList<PlangConflict> batch)
        {
            var fatal = new FatalError(MyPluginInfo.PLUGIN_NAME, Reason)
            {
                Action = Action,
            };

            foreach (PlangConflict conflict in batch)
            {
                fatal.Details.Add(conflict.Describe());

                AddCulprit(fatal, conflict.Kept);
                AddCulprit(fatal, conflict.Ignored);
            }

            PolarisAPI.Errors.Fatal(fatal);
        }

        static void AddCulprit(FatalError fatal, Assembly assembly)
        {
            // 一个模组和多个模组分别撞车时会被带进来多次，报告里只该出现一次。
            if (assembly != null && !fatal.Culprits.Contains(assembly))
            {
                fatal.Culprits.Add(assembly);
            }
        }

        static readonly FatalText Reason = new FatalText(
            english:
                "Two or more mods registered the same localization key. Which text wins depends on the "
                + "mod load order, so the game would show one mod's strings inside another mod's UI.",
            chinese:
                "有两个以上的模组注册了同一个本地化 key。哪一份文案生效取决于模组加载顺序，"
                + "游戏里会出现「一个模组的界面上显示着另一个模组的文字」这种错乱。",
            japanese:
                "同一のローカライズキーが複数のMODから登録されました。どのテキストが有効になるかは"
                + "MODの読み込み順に依存するため、あるMODのUIに別のMODの文字列が表示されてしまいます。");

        static readonly FatalText Action = new FatalText(
            english:
                "· Until it is fixed, keep only one of the mods listed above enabled (Polaris page on the title screen).\n"
                + "· Send this report to their authors: one side has to rename its key. Prefix .plang keys with your own "
                + "mod name (e.g. mymod.ok) and they can never collide.",
            chinese:
                "· 在修好之前，上面列出的模组只保留一个（在标题画面的 Polaris 页里关掉其余的）。\n"
                + "· 请把这份报告交给它们的作者：必须有一方改 key。给 .plang 的 key 统一加上自己的"
                + "模组名前缀（如 mymod.ok）就永远不会再撞。",
            japanese:
                "· 修正されるまでは、上記のMODのうち一つだけを有効にしてください（タイトル画面の Polaris ページ）。\n"
                + "· このレポートを各作者へご提出ください：どちらか一方がキー名を変更する必要があります。"
                + ".plang のキーに自身のMOD名の接頭辞（例：mymod.ok）を付ければ、衝突は起こりません。");
    }
}
