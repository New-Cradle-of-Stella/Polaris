using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using nel;
using nel.title;
using Polaris.Diagnostics;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>
    /// 标题画面的"上一局错误情况"告知页。
    /// <para>
    /// 错误几乎都发生在离开标题画面之后——崩溃、模组冲突、NullReferenceException 全部发生在
    /// 游戏进行中，标题本身很少出错——所以这一页展示的不是"现在"，而是"上一局"：
    /// 进程退出前（<see cref="Plugin"/> 的 <c>OnApplicationQuit</c>）把本局归档的错误摘要
    /// （<see cref="ErrorRegistry.Incidents"/>）写进 <see cref="PolarisNoticeStore"/>，
    /// 下次启动时在这里读出来显示；玩家按确认即清空，不确认就一直显示到确认为止。
    /// </para>
    /// <para>
    /// 建页方式、闸门写法、淡入动画都照抄 <see cref="PolarisModWarning"/>；与它共用
    /// <see cref="TitleOverlays"/> 这套注册表——本页存在的意义之一就是不必再为"多一页告知"
    /// 去改两个 Harmony 补丁。语言跟随玩家当前选择（不像警示页那样三语同屏）：
    /// 这里内置 zh/ja/en 三份文案、未知语言退回英文，理由见 <see cref="NoticeLocale"/>。
    /// </para>
    /// </summary>
    internal static class PolarisErrorNotice
    {
        internal static readonly ITitleOverlay Overlay = new OverlayAdapter();

        sealed class OverlayAdapter : ITitleOverlay
        {
            public bool Gate(SceneTitleTemp scene) => PolarisErrorNotice.Gate(scene);
            public void AdvanceFade(float deltaSeconds) => PolarisErrorNotice.AdvanceFade(deltaSeconds);
        }

        // ================== 持久化 ==================

        const string ConfigSection = "ErrorNotice";

        /// <summary>标题页最多列几条一行式摘要；再多就交给"另有 N 条，见报告文件"收尾。</summary>
        const int MaxPersistedLines = 5;

        static ConfigEntry<int> pendingCount;
        static ConfigEntry<string> pendingPath;
        static ConfigEntry<int> pendingMore;
        static readonly ConfigEntry<string>[] pendingLines = new ConfigEntry<string>[MaxPersistedLines];
        static bool configResolved;

        /// <summary>本次进程内已确认。配置写不了盘时靠它兜底，至少不会一局之内反复弹。</summary>
        static bool sessionAcknowledged;

        /// <summary>本次进程内建页失败过，不再重试。</summary>
        static bool buildFailed;

        /// <summary>
        /// 进程退出前调用：把本局归档的错误摘要写进配置，供下次启动的告知页读取。
        /// 本局没有任何模组相关错误时清空——告知页展示的必须永远是"上一局"，不能是"上上局"。
        /// </summary>
        internal static void PersistPending()
        {
            if (!ResolveEntries())
            {
                return;
            }

            IReadOnlyList<ErrorIncident> incidents = ErrorRegistry.Incidents;

            pendingCount.Value = incidents.Count;
            pendingPath.Value = incidents.Count > 0 ? ErrorReportWriter.LastWrittenPath ?? "" : "";
            pendingMore.Value = Math.Max(0, incidents.Count - MaxPersistedLines);

            for (int i = 0; i < pendingLines.Length; i++)
            {
                pendingLines[i].Value = i < incidents.Count ? incidents[i].OneLine() : "";
            }

            PolarisNoticeStore.File?.Save();
        }

        /// <summary>
        /// 进程启动时调用：上一局没有正常结束时，把 <see cref="SessionSentinel"/> 读出来的结局
        /// 接进告知页要用的待读状态，效果等同于"替上一局补一次它没机会做的
        /// <see cref="PersistPending"/>"。
        /// <para>
        /// 上一局正常退出（或这是第一次运行）时 <paramref name="last"/> 为 null，什么都不做——
        /// 那种情况下 <see cref="PersistPending"/> 已经在上一局的 <c>OnApplicationQuit</c> 里
        /// 把该写的都写了，这里再写一遍只会用一份过时的空快照把它覆盖掉。
        /// </para>
        /// </summary>
        internal static void AdoptLastSession(LastSessionInfo last)
        {
            if (last == null || last.Kind != SessionEndKind.Hung && last.Kind != SessionEndKind.NotClosed)
            {
                return;
            }

            if (!ResolveEntries())
            {
                return;
            }

            // 卡死/未正常退出这件事本身放在第一行——哪怕上一局一条模组错误都没归档过
            // （纯粹卡死，或者异常发生在能捕获它之前），玩家也得先看到这一条。
            var lines = new List<string>(MaxPersistedLines) { last.OneLine() };
            int more = last.MoreErrorKinds;

            foreach (string line in last.ErrorLines)
            {
                if (lines.Count < MaxPersistedLines)
                {
                    lines.Add(line);
                }
                else
                {
                    more++;
                }
            }

            pendingCount.Value = last.ErrorKinds + 1;
            pendingPath.Value = last.ReportPath ?? "";
            pendingMore.Value = more;

            for (int i = 0; i < pendingLines.Length; i++)
            {
                pendingLines[i].Value = i < lines.Count ? lines[i] : "";
            }

            PolarisNoticeStore.File?.Save();
        }

        static bool IsPending
        {
            get
            {
                if (sessionAcknowledged || buildFailed)
                {
                    return false;
                }

                // 玩家关掉的是"弹这一页"，不是"记录错误"：待读状态照旧留在配置里
                // （报告文件也照常写），重新打开之后上一局的摘要还在，不会被这次关闭吞掉。
                if (!Settings.PolarisSettings.ShowErrorNotice)
                {
                    return false;
                }

                return ResolveEntries() && pendingCount.Value > 0;
            }
        }

        static bool ResolveEntries()
        {
            if (configResolved)
            {
                return pendingCount != null;
            }

            configResolved = true;

            ConfigFile file = PolarisNoticeStore.File;
            if (file == null)
            {
                return false;
            }

            try
            {
                pendingCount = file.Bind(ConfigSection, "PendingCount", 0,
                    "上一局归档的模组相关错误种类数。标题画面告知页据此判断要不要弹出；" +
                    "玩家确认后归零。");
                pendingPath = file.Bind(ConfigSection, "PendingReportPath", "",
                    "上一局写出的报告文件路径。");
                pendingMore = file.Bind(ConfigSection, "PendingMoreCount", 0,
                    $"超出下面 {MaxPersistedLines} 条摘要之外、未逐条列出的错误种类数。");

                for (int i = 0; i < pendingLines.Length; i++)
                {
                    pendingLines[i] = file.Bind(ConfigSection, $"PendingLine{i + 1}", "");
                }

                return true;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris] 绑定错误告知页的待读状态失败：{e}");
                pendingCount = null;
                return false;
            }
        }

        // ================== 布局 ==================

        const float ContentW = 760f;
        const float ContentMinSideMargin = 40f;

        const float HeadingH = 40f;
        const float SummaryH = 32f;
        const float LineH = 26f;
        const float MoreH = 24f;
        const float PathH = 40f;
        const float ButtonRowH = 56f;
        const float HintH = 32f;

        const float ButtonW = 260f;
        const float ButtonH = 38f;

        const float HeadingSize = 19f;
        const float SummarySize = 14f;
        const float LineSize = 13f;
        const float PathSize = 12f;
        const float HintSize = 13f;

        /// <summary>
        /// 整页在标题场景里的 z，取值与 <see cref="PolarisModWarning"/> 相同——两页
        /// 从不同框出现（<see cref="TitleOverlays"/> 保证同一时刻只有一页在拦），不会叠。
        /// </summary>
        const float OverlayZ = -3f;

        const uint BackdropColor = 0xF0000000u;

        static readonly Color32 HeadingColor = new Color32(255, 255, 255, 255);
        static readonly Color32 SummaryColor = new Color32(238, 238, 238, 255);
        static readonly Color32 LineColor = new Color32(206, 206, 206, 255);
        static readonly Color32 MoreColor = new Color32(178, 178, 178, 255);
        static readonly Color32 PathColor = new Color32(150, 150, 150, 255);
        static readonly Color32 HintColor = new Color32(200, 200, 200, 255);
        static readonly Color32 BorderColor = C32.d2c(3707764736u);

        const float FadeSeconds = 0.22f;

        // ================== 文案（跟随玩家当前语言，未知语言退回英文） ==================

        readonly struct Wording
        {
            public Wording(string heading, string summaryFormat, string moreFormat, string pathLabel, string confirm, string hint)
            {
                Heading = heading;
                SummaryFormat = summaryFormat;
                MoreFormat = moreFormat;
                PathLabel = pathLabel;
                Confirm = confirm;
                Hint = hint;
            }

            public string Heading { get; }
            public string SummaryFormat { get; }
            public string MoreFormat { get; }
            public string PathLabel { get; }
            public string Confirm { get; }
            public string Hint { get; }
        }

        static readonly Wording ZhWording = new Wording(
            "上一局的错误报告",
            "上次运行记录到 {0} 类相关问题：",
            "……另有 {0} 类，见完整报告",
            "完整报告：",
            "我知道了",
            $"{KeyHint.Submit} 确认");

        static readonly Wording JaWording = new Wording(
            "前回のエラーレポート",
            "前回の実行で {0} 件の問題が記録されました：",
            "……ほか {0} 件、詳細はレポートを参照",
            "詳細レポート：",
            "了解しました",
            $"{KeyHint.Submit} 決定");

        static readonly Wording EnWording = new Wording(
            "Previous Run's Error Report",
            "The last run recorded {0} issue(s):",
            "...and {0} more, see the full report",
            "Full report:",
            "GOT IT",
            $"{KeyHint.Submit} confirm");

        /// <summary>按当前语言族选文案；未识别的一律退回英文（判定见 <see cref="NoticeLocale"/>）。</summary>
        static Wording CurrentWording()
        {
            switch (NoticeLocale.Current)
            {
                case NoticeLanguage.Chinese: return ZhWording;
                case NoticeLanguage.Japanese: return JaWording;
                default: return EnWording;
            }
        }

        // ================== 状态 ==================

        static GameObject host;
        static Designer designer;
        static float fade;

        /// <summary>每帧从原版闸门问过来一次：返回 true 表示本页仍要拦住标题菜单。首次调用时建页。</summary>
        internal static bool Gate(SceneTitleTemp scene)
        {
            if (!IsPending)
            {
                return false;
            }

            if (designer == null && !TryBuild(scene))
            {
                buildFailed = true;
                return false;
            }

            return true;
        }

        internal static void AdvanceFade(float deltaSeconds)
        {
            if (designer == null || fade >= 1f)
            {
                return;
            }

            fade = Mathf.Min(1f, fade + deltaSeconds / FadeSeconds);
            designer.alpha = fade;
        }

        // ================== 建页 ==================

        static bool TryBuild(SceneTitleTemp scene)
        {
            try
            {
                Build(scene);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris] 错误告知页构建失败，本局跳过：{e}");
                Teardown();
                return false;
            }
        }

        static void Build(SceneTitleTemp scene)
        {
            Wording w = CurrentWording();
            int count = pendingCount?.Value ?? 0;
            int shown = Math.Min(count, MaxPersistedLines);
            int more = pendingMore?.Value ?? 0;
            string path = pendingPath?.Value;
            bool hasPath = !string.IsNullOrEmpty(path);

            float screenW = IN.wh * 2f;
            float screenH = IN.hh * 2f;
            float contentW = Mathf.Min(ContentW, screenW - ContentMinSideMargin * 2f);
            float contentH = HeadingH + SummaryH + shown * LineH
                              + (more > 0 ? MoreH : 0f)
                              + (hasPath ? PathH : 0f)
                              + ButtonRowH + HintH;

            host = IN.CreateGob(scene.gameObject, "-polaris_error_notice");
            IN.setZ(host.transform, OverlayZ);

            designer = host.AddComponent<Designer>();
            designer.Smallest();
            designer.WH(screenW, screenH);
            designer.bgcol = C32.d2c(BackdropColor);
            designer.margin_in_lr = (screenW - contentW) / 2f;
            designer.margin_in_tb = Mathf.Max(0f, (screenH - contentH) / 2f);
            designer.alignx = ALIGN.CENTER;
            designer.init();

            MFont font = TX.getDefaultFont();

            AddParagraph(w.Heading, HeadingH, HeadingSize, HeadingColor, font, border: true);
            AddParagraph(string.Format(w.SummaryFormat, count), SummaryH, SummarySize, SummaryColor, font, border: false);

            for (int i = 0; i < shown; i++)
            {
                string line = pendingLines[i]?.Value;
                if (!string.IsNullOrEmpty(line))
                {
                    AddParagraph("· " + Clip(line, 70), LineH, LineSize, LineColor, font, border: false);
                }
            }

            if (more > 0)
            {
                AddParagraph(string.Format(w.MoreFormat, more), MoreH, LineSize, MoreColor, font, border: false);
            }

            if (hasPath)
            {
                AddParagraph(w.PathLabel + Clip(path, 90), PathH, PathSize, PathColor, font, border: false);
            }

            aBtn confirm = designer.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "polaris_error_notice_confirm",
                skin = "normal_dark",
                title = w.Confirm,
                w = ButtonW,
                h = ButtonH,
                fnClick = _ =>
                {
                    Confirm();
                    return true;
                }
            });
            designer.Br();

            AddParagraph(w.Hint, HintH, HintSize, HintColor, font, border: true, html: true);

            designer.activate();
            confirm.Select();

            fade = 0f;
            designer.alpha = 0f;

            IN.clearPushDown(strong: true);
        }

        static void AddParagraph(
            string text, float height, float size, Color32 color, MFont font, bool border, bool html = false)
        {
            designer.addP(new DsnDataP(text, html)
            {
                size = size,
                alignx = ALIGN.CENTER,
                aligny = ALIGNY.MIDDLE,
                TxCol = color,
                TxBorderCol = border ? BorderColor : default,
                TargetFont = font,
                swidth = designer.use_w,
                sheight = height,
                text_auto_wrap = true,
                lineSpacing = 1.15f,
                do_not_error_unknown_tag = true,
            });
            designer.Br();
        }

        static string Clip(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max) + "…";
        }

        // ================== 确认与收尾 ==================

        static void Confirm()
        {
            sessionAcknowledged = true;
            Teardown();

            try
            {
                if (ResolveEntries())
                {
                    pendingCount.Value = 0;
                    pendingPath.Value = "";
                    pendingMore.Value = 0;
                    foreach (ConfigEntry<string> line in pendingLines)
                    {
                        line.Value = "";
                    }

                    PolarisNoticeStore.File?.Save();
                }
            }
            catch (Exception e)
            {
                // 落不了盘只影响"下次还弹一次"，不影响本局。
                Plugin.Logger.LogWarning($"[Polaris] 错误告知页的确认状态没能写入配置：{e.Message}");
            }
        }

        static void Teardown()
        {
            designer = null;
            fade = 0f;

            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
                host = null;
            }
        }
    }
}
