using System.Collections.Generic;
using System.Reflection;
using Polaris.Diagnostics;

namespace Polaris.Lang
{
    /// <summary>
    /// key 冲突的收集与处置：<b>只要有一个 key 被两个模组重复注册，这一局就不再继续</b>——
    /// 交给 <see cref="PolarisAPI.Errors"/> 的致命错误通道写出报告，并在标题
    /// 画面拦住菜单、请玩家退出游戏。
    /// <para>
    /// 为什么这么重：key 撞车的后果不是"少了一句文案"，而是<b>界面上出现另一个模组的文字</b>，
    /// 而且哪一份生效取决于模组加载顺序——换一次加载顺序表现就变一次。玩家看到的是"某个
    /// 模组的界面串台了"，几乎不可能自己追回到"两份 <c>.plang</c> 用了同一个 key"，报到哪个
    /// 作者那里都会被当成"我这边没问题"。这种问题必须在它产生第一份错误截图之前就停下来。
    /// </para>
    /// <para>
    /// 处置只做一次：扫描期间收集，扫描结束时汇总成一条致命错误（<see cref="Seal"/>）——
    /// 十个冲突弹十次页面毫无意义，一条里列全反而看得清。扫描结束之后才出现的冲突
    /// （有人在运行期直接调 <see cref="PlangRuntime.Register"/>）当场单独报一条。
    /// </para>
    /// </summary>
    internal static class PlangConflictGuard
    {
        static readonly List<PlangConflict> conflicts = new();

        /// <summary>扫描已经结束过一次，此后的冲突当场上报。</summary>
        static bool scanFinished;

        /// <summary>
        /// 当前正在执行注册的那个生成类所属的程序集，由 <see cref="PlangRegistryScanner"/>
        /// 在调用 <see cref="IPlangRegistrar.Register"/> 前后设置/清空。
        /// <para>
        /// 用环境变量式的传递、而不是给 <see cref="PlangRuntime.Register"/> 加一个 assembly 参数：
        /// 那个方法的调用方是 PolarisTools 生成的代码，改它的签名等于要求所有下游模组重新生成
        /// 一遍代码才能升级 Polaris。<see cref="PlangRuntime.Register"/> 在这里为空时会退回
        /// <c>Assembly.GetCallingAssembly()</c>，两条路都指向同一个答案。
        /// </para>
        /// </summary>
        internal static Assembly CurrentSource { get; set; }

        /// <summary>本局记录到的冲突，按发现顺序。</summary>
        internal static IReadOnlyList<PlangConflict> Conflicts => conflicts;

        /// <summary>
        /// 记一次冲突。<paramref name="kept"/> 是先注册、文案被保留的一方——
        /// 保留先来的那一份而不是让后来者覆盖，是为了让"哪一份生效"至少在同一次启动内是稳定的，
        /// 不至于在退出游戏之前的这段时间里再多一种表现。
        /// </summary>
        internal static void Record(string key, Assembly kept, Assembly ignored)
        {
            var conflict = new PlangConflict(key, kept, ignored);
            conflicts.Add(conflict);

            // 用 LogError 而不是 LogFatal：LogFatal 会被 Polaris 的日志监听器当成
            // "插件报出的严重错误"再建一条普通错误档，同一件事在报告里出现两遍——
            // 这件事的权威记录是下面 Errors.Fatal 写出的那一段。
            Plugin.Logger.LogError($"[PolarisLang] key conflict: {conflict.Describe()}");

            if (scanFinished)
            {
                RaiseFatal(new[] { conflict });
            }
        }

        /// <summary>
        /// 扫描结束时调用一次：有冲突就汇总成一条致命错误上报。
        /// </summary>
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
            // 一个模组和好几个模组分别撞车时会被带进来多次，报告里只该出现一次。
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
