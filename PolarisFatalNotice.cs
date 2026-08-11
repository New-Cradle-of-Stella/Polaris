using System;
using nel;
using nel.title;
using Polaris.Diagnostics;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>
    /// 标题画面的致命错误页：本局被 <see cref="Infra.ErrorsAPI.Fatal"/> 判定为不能继续时，
    /// 把原因摆到玩家面前，并且只给一个出口——退出游戏。
    /// <para>
    /// 与 <see cref="PolarisErrorNotice"/>（讲"上一局出了什么问题"）相反，这一页讲的是"这一局
    /// 现在就到此为止"：致命错误几乎都在模块初始化阶段被发现（比如两个模组撞了同一个本地化
    /// key），那时标题场景还没建出来，直接退进程玩家只会看到"点了启动，游戏闪一下没了"。
    /// 所以判定与展示分开：<see cref="FatalRegistry"/> 在发现的那一刻就把日志和报告落好，
    /// 这一页等标题画面起来之后再告诉玩家发生了什么。
    /// </para>
    /// <para>
    /// 建页方式、闸门写法、淡入动画都照抄 <see cref="PolarisModWarning"/>，并和它一起挂在
    /// <see cref="TitleOverlays"/> 上；本页排在最前面——一个已经判死刑的环境，没必要再问玩家
    /// "上一局的错误看没看过"。唯一的实质区别是<b>永远不会被确认掉</b>：
    /// <see cref="Gate"/> 只要还有致命错误就一直返回 true，玩家碰不到"开始游戏 / 读取"
    /// 那一排按钮，按钮点下去走的是 <see cref="MainMenuAPI.QuitGame"/>。
    /// </para>
    /// </summary>
    internal static class PolarisFatalNotice
    {
        internal static readonly ITitleOverlay Overlay = new OverlayAdapter();

        sealed class OverlayAdapter : ITitleOverlay
        {
            public bool Gate(SceneTitleTemp scene) => PolarisFatalNotice.Gate(scene);
            public void AdvanceFade(float deltaSeconds) => PolarisFatalNotice.AdvanceFade(deltaSeconds);
        }

        // ================== 布局 ==================

        const float ContentW = 860f;
        const float ContentMinSideMargin = 40f;

        const float HeadingH = 44f;

        /// <summary>
        /// 原因与"该怎么办"两段的高度按<b>最长的那门语言</b>留：文案由调用方给，日文和英文
        /// 往往比中文长出一两行，按中文的行数留会让另外两种语言撞出框外。
        /// </summary>
        const float ReasonH = 66f;

        const float DetailH = 24f;
        const float MoreH = 24f;
        const float ActionH = 88f;
        const float PathH = 40f;
        const float ButtonRowH = 56f;
        const float HintH = 32f;

        const float ButtonW = 300f;
        const float ButtonH = 38f;

        const float HeadingSize = 20f;
        const float ReasonSize = 15f;
        const float DetailSize = 13f;
        const float ActionSize = 13.5f;
        const float PathSize = 12f;
        const float HintSize = 13f;

        /// <summary>明细最多列几条；其余归到"另有 N 条，见报告"。</summary>
        const int MaxDetailLines = 6;

        /// <summary>单条明细的字数上限，超出截断。明细是 key 名/dll 名，长的那种撑破一行也没意义。</summary>
        const int DetailClip = 78;

        /// <summary>z 与另外两页相同：<see cref="TitleOverlays"/> 保证同一刻只有一页在拦，不会叠。</summary>
        const float OverlayZ = -3f;

        /// <summary>
        /// 底板比另外两页更实（0xF8 对 0xF0）：那两页确认完游戏照常玩，透出后面的 logo 是
        /// 一种"马上就回去"的暗示；这一页之后没有"回去"，不该再给这个暗示。
        /// </summary>
        const uint BackdropColor = 0xF8000000u;

        static readonly Color32 HeadingColor = new Color32(255, 196, 196, 255);
        static readonly Color32 ReasonColor = new Color32(240, 240, 240, 255);
        static readonly Color32 DetailColor = new Color32(206, 206, 206, 255);
        static readonly Color32 MoreColor = new Color32(178, 178, 178, 255);
        static readonly Color32 ActionColor = new Color32(226, 226, 226, 255);
        static readonly Color32 PathColor = new Color32(150, 150, 150, 255);
        static readonly Color32 HintColor = new Color32(200, 200, 200, 255);
        static readonly Color32 BorderColor = C32.d2c(3707764736u);

        const float FadeSeconds = 0.22f;

        // ================== 文案（跟随玩家当前语言，未知语言退回英文） ==================

        readonly struct Wording
        {
            public Wording(string heading, string sourceFormat, string moreDetailsFormat,
                string otherFatalsFormat, string pathLabel, string pathMissing, string quit, string hint)
            {
                Heading = heading;
                SourceFormat = sourceFormat;
                MoreDetailsFormat = moreDetailsFormat;
                OtherFatalsFormat = otherFatalsFormat;
                PathLabel = pathLabel;
                PathMissing = pathMissing;
                Quit = quit;
                Hint = hint;
            }

            public string Heading { get; }

            /// <summary>{0} = 报出这条致命错误的模块名。</summary>
            public string SourceFormat { get; }

            public string MoreDetailsFormat { get; }
            public string OtherFatalsFormat { get; }
            public string PathLabel { get; }
            public string PathMissing { get; }
            public string Quit { get; }
            public string Hint { get; }
        }

        static readonly Wording ZhWording = new Wording(
            "无法继续：模组环境有问题",
            "由 {0} 判定",
            "……另有 {0} 条明细，见报告文件",
            "本局还报出了另外 {0} 条致命错误，同样见报告文件",
            "完整报告：",
            "（报告文件写入失败，详情见 BepInEx/LogOutput.log）",
            "退出游戏",
            $"{KeyHint.Submit} 退出");

        static readonly Wording JaWording = new Wording(
            "続行不可：MOD環境に問題があります",
            "{0} による判定",
            "……ほか {0} 件、詳細はレポートを参照",
            "今回はさらに {0} 件の致命的エラーが報告されました（レポート参照）",
            "詳細レポート：",
            "（レポートの書き込みに失敗しました。BepInEx/LogOutput.log をご確認ください）",
            "ゲームを終了する",
            $"{KeyHint.Submit} 終了");

        static readonly Wording EnWording = new Wording(
            "CANNOT CONTINUE: BROKEN MOD SETUP",
            "reported by {0}",
            "...and {0} more, see the report file",
            "{0} more fatal error(s) were reported this run, see the report file",
            "Full report:",
            "(the report file could not be written, see BepInEx/LogOutput.log)",
            "QUIT GAME",
            $"{KeyHint.Submit} quit");

        static Wording CurrentWording()
        {
            switch (NoticeLocale.Current)
            {
                case NoticeLanguage.Chinese: return ZhWording;
                case NoticeLanguage.Japanese: return JaWording;
                default: return EnWording;
            }
        }

        static readonly FatalText DefaultAction = new FatalText(
            english: "Turn the mods listed in the report off, then start the game again.",
            chinese: "请把报告里列出的模组关掉，然后重新启动游戏。",
            japanese: "レポートに記載されたMODを無効化してから、ゲームを再起動してください。");

        // ================== 状态 ==================

        static GameObject host;
        static Designer designer;
        static float fade;

        /// <summary>建页失败过一次就不再重试，并且直接退出游戏（理由见 <see cref="Gate"/>）。</summary>
        static bool buildFailed;

        /// <summary>已经发出过退出请求，避免每帧重复请求。</summary>
        static bool quitRequested;

        /// <summary>
        /// 每帧从原版闸门问过来一次。有致命错误就一直返回 true——这一页没有"确认"，
        /// 玩家在标题画面唯一能做的事就是退出。
        /// <para>
        /// 建不出来时的处置和另外两页相反：那两页放行（少看一页提示远好过把玩家锁在菜单外），
        /// 这一页改为直接退出游戏。已经判定"这一局不能继续"，却因为自己画不出一个提示框就
        /// 让玩家进游戏，是把两个错误叠在一起。
        /// </para>
        /// </summary>
        internal static bool Gate(SceneTitleTemp scene)
        {
            if (!FatalRegistry.Any)
            {
                return false;
            }

            if (buildFailed)
            {
                RequestQuit();
                return false;
            }

            if (designer == null && !TryBuild(scene))
            {
                buildFailed = true;
                RequestQuit();
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
                Plugin.Logger.LogError($"[Polaris] Failed to build the fatal error page; quitting the game directly: {e}");
                Teardown();
                return false;
            }
        }

        static void Build(SceneTitleTemp scene)
        {
            Wording w = CurrentWording();
            NoticeLanguage language = NoticeLocale.Current;
            FatalError fatal = FatalRegistry.First;

            string reason = fatal?.Reason?.Pick(language) ?? "";
            string action = (fatal?.Action ?? DefaultAction).Pick(language);
            string source = string.Format(w.SourceFormat, fatal?.Source ?? "Polaris");

            int detailCount = fatal?.Details.Count ?? 0;
            int shownDetails = Math.Min(detailCount, MaxDetailLines);
            int moreDetails = detailCount - shownDetails;
            int otherFatals = FatalRegistry.OtherCount;

            string path = FatalRegistry.ReportPath;

            float screenW = IN.wh * 2f;
            float screenH = IN.hh * 2f;
            float contentW = Mathf.Min(ContentW, screenW - ContentMinSideMargin * 2f);
            float contentH = HeadingH + ReasonH + DetailH /* source 行 */
                             + shownDetails * DetailH
                             + (moreDetails > 0 ? MoreH : 0f)
                             + (otherFatals > 0 ? MoreH : 0f)
                             + ActionH + PathH + ButtonRowH + HintH;

            host = IN.CreateGob(scene.gameObject, "-polaris_fatal_notice");
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
            AddParagraph(reason, ReasonH, ReasonSize, ReasonColor, font, border: false);
            AddParagraph(source, DetailH, DetailSize, MoreColor, font, border: false);

            for (int i = 0; i < shownDetails; i++)
            {
                AddParagraph("· " + Clip(fatal.Details[i], DetailClip),
                    DetailH, DetailSize, DetailColor, font, border: false);
            }

            if (moreDetails > 0)
            {
                AddParagraph(string.Format(w.MoreDetailsFormat, moreDetails),
                    MoreH, DetailSize, MoreColor, font, border: false);
            }

            if (otherFatals > 0)
            {
                AddParagraph(string.Format(w.OtherFatalsFormat, otherFatals),
                    MoreH, DetailSize, MoreColor, font, border: false);
            }

            AddParagraph(action, ActionH, ActionSize, ActionColor, font, border: false);
            AddParagraph(path != null ? w.PathLabel + Clip(path, 90) : w.PathMissing,
                PathH, PathSize, PathColor, font, border: false);

            aBtn quit = designer.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "polaris_fatal_notice_quit",
                skin = "normal_dark",
                title = w.Quit,
                w = ButtonW,
                h = ButtonH,
                fnClick = _ =>
                {
                    RequestQuit();
                    return true;
                }
            });
            designer.Br();

            AddParagraph(w.Hint, HintH, HintSize, HintColor, font, border: true, html: true);

            designer.activate();
            quit.Select();

            fade = 0f;
            designer.alpha = 0f;

            // 玩家在 logo 淡入期间按下的确定键此刻还没消费掉，不清掉的话退出按钮会在同一帧
            // 被这次按下直接点掉——那就等于没让人看见这一页（同 PolarisModWarning）。
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

        // ================== 退出 ==================

        /// <summary>
        /// 走 <see cref="MainMenuAPI.QuitGame"/>（也就是原版"退出游戏"那条路：淡出、存档收尾、
        /// <c>IN.quitGame()</c>），不自己拆 <c>Application.Quit</c>。页面留在原地不销毁——
        /// 淡出的那几十帧里玩家应该还能看到自己为什么被赶出来。
        /// </summary>
        static void RequestQuit()
        {
            if (quitRequested)
            {
                return;
            }

            quitRequested = true;

            try
            {
                PolarisAPI.MainMenu.QuitGame();
            }
            catch (Exception e)
            {
                // 走不了原版流程也必须退出去，否则玩家被永久锁在标题菜单之外，还不知道为什么。
                Plugin.Logger.LogError($"[Polaris] Failed to use the vanilla quit path; terminating the process directly: {e}");
                Application.Quit();
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
