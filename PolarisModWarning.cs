using System;
using BepInEx.Configuration;
using nel;
using nel.title;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>
    /// 一次性的模组环境警示页：仿原版首次启动的敏感内容告知页（<c>SceneTitleTemp</c> 的
    /// <c>STATE.SENSITIVE_ANNOUNCE</c>，文案 key <c>Title_Announce_For_Sensitive</c>）——
    /// 全屏暗底、居中正文、下方一个语言切换按钮加一个确认按钮，再加一行按键提示。内容是告诉
    /// 玩家：这份游戏跑在模组环境下，出了问题先自己排查，别拿去找游戏原作者。
    /// <para>
    /// 一次只显示一种语言，不跟随游戏语言设置——默认英语，玩家可以点语言按钮在
    /// 英/中/日之间循环切换。之所以不跟随游戏当前语言：这一页的责任声明必须对所有玩家
    /// 都可见，而"跟随游戏语言"意味着装了英文本体的玩家永远看不到中文版、反之亦然；
    /// 默认英语则是因为 Steam 创意工坊/英文社区是目前最主要的模组分发渠道。
    /// </para>
    /// <para>
    /// 插入位置借用了原版自己的闸门 <c>SceneTitleTemp.errorAnnounceBox</c>（见
    /// <see cref="Patch.Patch_SceneTitleTemp_errorAnnounceBox"/>）：标题状态机进入
    /// <c>STATE.TOP</c> 之后、顶部按钮行 <c>BxTop</c> 激活之前，原版每帧都会问一次
    /// "有没有告知框要先弹"，返回 true 就不激活按钮行。本页把这一问接管过来，于是它出现的
    /// 时机与原版的错误告知框完全一致——玩家在确认之前碰不到"开始游戏/读取"那一排按钮。
    /// </para>
    /// <para>
    /// 确认状态落在 <c>BepInEx/config/Polaris/_polaris_notice.cfg</c>，确认一次之后永远不再弹；
    /// 想再看一次把该文件删掉（或把值改回 false）即可。语言选择不落盘——纯粹是本次显示期间的
    /// 临时状态，每次进程重启都重新从英语开始。
    /// </para>
    /// </summary>
    internal static class PolarisModWarning
    {
        /// <summary>
        /// 供 <see cref="TitleOverlays"/> 调用的适配器：本类沿用一贯的全静态写法
        /// （建页状态、淡入进度都是进程级单例，没有必要包一层实例），接口本身要求实例方法，
        /// 用一个不持有任何状态的适配器转发即可。
        /// </summary>
        internal static readonly ITitleOverlay Overlay = new OverlayAdapter();

        sealed class OverlayAdapter : ITitleOverlay
        {
            public bool Gate(SceneTitleTemp scene) => PolarisModWarning.Gate(scene);
            public void AdvanceFade(float deltaSeconds) => PolarisModWarning.AdvanceFade(deltaSeconds);
        }

        // ================== 持久化 ==================
        // 配置文件是 PolarisErrorNotice 共用的同一个 _polaris_notice.cfg，见 PolarisNoticeStore
        // 上的说明——两页各自 Bind 自己的键，但必须经同一个 ConfigFile 实例存取。

        const string ConfigSection = "Notice";
        const string ConfigKey = "ModEnvironmentWarningAcknowledged";

        static ConfigEntry<bool> acknowledged;
        static bool configResolved;

        /// <summary>本次进程内已确认。配置文件打不开时靠它兜底，至少不会一局之内反复弹。</summary>
        static bool sessionAcknowledged;

        /// <summary>本次进程内建页失败过。失败不写确认标记——下次启动还应该让玩家看到这一页。</summary>
        static bool buildFailed;

        // ================== 布局 ==================
        // 尺寸全部是像素（原版 UI 的通用单位，IN.w = 1280 / IN.h = 720 是逻辑分辨率，
        // IN.wh / IN.hh 是实际视口的半宽半高，会随窗口比例变化——底板要盖满屏幕必须用后者）。

        const float ContentW = 960f;
        const float ContentMinSideMargin = 40f;

        const float LangRowH = 34f;
        const float HeadingH = 44f;
        const float ConfirmRowH = 56f;
        const float HintH = 32f;

        const float LangButtonW = 90f;
        const float LangButtonH = 26f;
        const float ConfirmButtonW = 400f;
        const float ButtonH = 38f;

        /// <summary>
        /// 语言按钮行与标题行之间的额外垂直间距。两行是先后两次 <c>Br()</c> 出来的，
        /// 默认紧挨着（<see cref="Designer.Smallest"/> 把 <c>item_margin_y_px</c> 清成了 0），
        /// 加标题前临时调大、加完立刻改回去，不影响 Body/确认按钮/Hint 之间的默认间距。
        /// </summary>
        const float LangGapY = 12f;

        const float HeadingSize = 20f;
        const float HintSize = 13f;

        /// <summary>
        /// 整页在标题场景里的 z。越负越靠前：标题常驻 UI 最靠前的是语言按钮 -0.2，
        /// 模组管理页整族在 -0.5，原版首次启动询问在 -2，按键设置在 -4.25。取 -3 稳稳盖住
        /// 标题画面的一切，又不越过按键设置——虽然本页只在 TOP 状态出现、两者不会同框。
        /// </summary>
        const float OverlayZ = -3f;

        /// <summary>底板颜色，0xF0 的黑：留一丝透明度，隐约透出后面正在淡入的标题 logo。</summary>
        const uint BackdropColor = 0xF0000000u;

        static readonly Color32 HeadingColor = new Color32(255, 255, 255, 255);
        static readonly Color32 BodyColor = new Color32(220, 220, 220, 255);
        static readonly Color32 HintColor = new Color32(200, 200, 200, 255);

        /// <summary>正文描边色，抄原版 TxOnePoint 的 <c>BorderCol(3707764736u)</c>。</summary>
        static readonly Color32 BorderColor = C32.d2c(3707764736u);

        /// <summary>淡入时长（秒）。原版那一页是按帧数走 X.ZLINE 的，这里按真实时间，效果一致。</summary>
        const float FadeSeconds = 0.22f;

        // ================== 文案（每种语言各自独立成页，按钮切换） ==================

        /// <summary>
        /// Polaris 自身错误报告的提交去处，取自 <see cref="PolarisMeta.ReportTarget"/>——
        /// 错误报告文件的结尾用的是同一个常量，两处口径永远一致。刻意不带尖括号：
        /// 正文块是纯文本模式，但换成 html 块时不至于被吃掉。
        /// </summary>
        const string ReportTarget = PolarisMeta.ReportTarget;

        /// <summary>一种语言的完整页面文案 + 排版参数 + 取字体用的语言族 key。</summary>
        readonly struct Wording
        {
            public Wording(string heading, string body, float bodyH, float bodySize,
                string confirmLabel, string switchLabel, string hint, string fontFamily)
            {
                Heading = heading;
                Body = body;
                BodyH = bodyH;
                BodySize = bodySize;
                ConfirmLabel = confirmLabel;
                SwitchLabel = switchLabel;
                Hint = hint;
                FontFamily = fontFamily;
            }

            public string Heading { get; }
            public string Body { get; }
            public float BodyH { get; }
            public float BodySize { get; }
            public string ConfirmLabel { get; }

            /// <summary>语言按钮上显示的文字——切过去之后会变成哪种语言，而不是"当前是哪种"。</summary>
            public string SwitchLabel { get; }
            public string Hint { get; }
            public string FontFamily { get; }
        }

        static readonly Wording EnglishWording = new Wording(
            "MODDED GAME NOTICE",
            "This copy is modded, so it no longer behaves like the original. A crash, a freeze, a broken save or anything else odd is not automatically a bug in the base game.\n" +
            "Check it yourself first: turn the suspect mods off from the Polaris page and restart; if it still happens with every mod disabled, confirm it once on a clean, unmodded copy. Until then, do not report it to the game's original author or the official channels. Report confirmed mod issues to that mod's author.\n" +
            "If Polaris itself produces an error report, please submit that to " + ReportTarget + ".",
            210f, 15f, "I UNDERSTAND", "中文", $"{KeyHint.Submit} confirm", EnglishFamily);

        static readonly Wording ChineseWording = new Wording(
            "模组环境提示",
            "你的游戏装了模组，运行结果和原版并不一致。崩溃、卡死、存档损坏或任何奇怪的表现，都不能默认是游戏本体的问题。\n" +
            "请先自己排查：在标题画面的 Polaris 页里关掉可疑的模组，重启看问题是否还在；全部模组都关掉后仍然复现，再用一份干净的游戏本体确认一次。在这之前请不要把问题反馈给游戏原作者或官方渠道；确认是某个模组导致的，请反馈给该模组的作者。\n" +
            "如果 Polaris 自己弹出了错误报告，请把它提交到 " + ReportTarget + "。",
            160f, 16f, "我已了解", "日本語", $"{KeyHint.Submit} 确认", ChineseFamily);

        static readonly Wording JapaneseWording = new Wording(
            "MOD環境について",
            "このゲームにはMODが導入されており、挙動はオリジナルと同じではありません。クラッシュ・フリーズ・セーブデータの破損など、おかしな症状がゲーム本体の不具合とは限りません。\n" +
            "まずご自身で切り分けてください：タイトル画面の Polaris ページで疑わしいMODを無効化して再起動し、すべてのMODを無効にしても再現する場合は、MODを一切入れていない状態でもう一度確認してください。それまではゲームの原作者や公式の窓口へ報告しないでください。MODが原因と判明した場合は、そのMODの作者へご報告ください。\n" +
            "Polaris 自体がエラーレポートを出力した場合は、" + ReportTarget + " へご提出ください。",
            210f, 15.5f, "了解しました", "English", $"{KeyHint.Submit} 決定", JapaneseFamily);

        /// <summary>
        /// 循环顺序：英 → 中 → 日 → 英……<see cref="langIndex"/> 默认 0，也就是默认英语。
        /// </summary>
        static readonly Wording[] Wordings = [EnglishWording, ChineseWording, JapaneseWording];

        static int langIndex;

        static Wording Current => Wordings[langIndex];

        // ================== 状态 ==================

        static GameObject host;
        static Designer designer;
        static float fade;

        /// <summary>本页当前依附的标题场景，语言切换时用来在不重新经过原版闸门的情况下重建页面。</summary>
        static SceneTitleTemp currentScene;

        /// <summary>这一页是否还没被玩家确认过（也就是还该不该拦住标题菜单）。</summary>
        static bool IsPending
        {
            get
            {
                if (sessionAcknowledged || buildFailed)
                {
                    return false;
                }

                // 配置读不出来时 Entry 为 null，按"没确认过"处理：宁可多弹一次，
                // 也不要因为写不了盘就把这页永远吞掉。
                return ResolveEntry()?.Value != true;
            }
        }

        /// <summary>
        /// 每帧从原版闸门问过来一次：返回 true 表示本页仍要拦住标题菜单。首次调用时建页。
        /// </summary>
        internal static bool Gate(SceneTitleTemp scene)
        {
            if (!IsPending)
            {
                return false;
            }

            // designer 是 UnityEngine.Object：回标题时场景被重建，旧实例已销毁，
            // 这里的 == null 会如实返回 true，于是自动重建一份。
            if (designer == null && !TryBuild(scene))
            {
                // 建不出来就放行。把玩家永久锁在标题菜单之外，比少看一页提示严重得多。
                buildFailed = true;
                return false;
            }

            return true;
        }

        /// <summary>推进淡入动画；由 <see cref="Patch.Patch_SceneTitleTemp_runIRD"/> 每帧调用。</summary>
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
                Plugin.Logger.LogError($"[Polaris] 模组警示页构建失败，本局跳过：{e}");
                Teardown();
                return false;
            }
        }

        static void Build(SceneTitleTemp scene)
        {
            currentScene = scene;
            Wording w = Current;

            float screenW = IN.wh * 2f;
            float screenH = IN.hh * 2f;
            float contentW = Mathf.Min(ContentW, screenW - ContentMinSideMargin * 2f);
            float contentH = LangRowH + LangGapY + HeadingH + w.BodyH + ConfirmRowH + HintH;

            // 挂在标题场景对象下面：场景卸载时跟着一起销毁，不需要自己管生命周期。
            // CreateGob 会连 layer/tag 一起继承过来，这是原版 UI 能被 GUI 相机拍到的前提。
            host = IN.CreateGob(scene.gameObject, "-polaris_mod_warning");
            IN.setZ(host.transform, OverlayZ);

            designer = host.AddComponent<Designer>();

            // Smallest() 一次性清掉圆角、行间距和出现动画的缩放，下面只写回真正需要的项。
            designer.Smallest();
            designer.WH(screenW, screenH);
            designer.bgcol = C32.d2c(BackdropColor);
            designer.margin_in_lr = (screenW - contentW) / 2f;
            designer.margin_in_tb = Mathf.Max(0f, (screenH - contentH) / 2f);
            designer.alignx = ALIGN.CENTER;
            designer.init();

            MFont font = ResolveFont(w.FontFamily);

            // 语言切换独占最上面一行，离标题/正文远远的，不会再跟确认按钮的皮肤装饰
            // 挤在一起。加标题前把 item_margin_y_px 调大一点点留出空隙，加完立刻改回去，
            // 不影响 Body/确认按钮/Hint 之间的默认间距。
            designer.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "polaris_warning_lang",
                skin = "normal_dark",
                title = w.SwitchLabel,
                w = LangButtonW,
                h = LangButtonH,
                fnClick = _ =>
                {
                    SwitchLanguage();
                    return true;
                }
            });
            designer.Br();

            designer.item_margin_y_px = LangGapY;

            AddParagraph(w.Heading, HeadingH, HeadingSize, HeadingColor, font, border: true);

            designer.item_margin_y_px = 0f;

            AddParagraph(w.Body, w.BodyH, w.BodySize, BodyColor, font, border: false);

            aBtn confirm = designer.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "polaris_warning_confirm",
                skin = "normal_dark", // 原版那一页的两个按钮用的就是这个深色皮肤
                title = w.ConfirmLabel,
                w = ConfirmButtonW,
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

            // alpha 必须在所有块都加完之后再设：setter 是遍历当前已有的块逐个下发的，
            // 先设后加的块拿不到这个值，会以 alpha=1 直接跳出来。
            fade = 0f;
            designer.alpha = 0f;

            // 玩家在 logo 淡入期间按下确定键也会立刻触发本页（原版闸门里的 IN.kettei3()），
            // 那一下按键此刻还没消费掉，不清掉的话确认按钮会在同一帧被这次按下直接点掉。
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
                // 显式写死：DsnDataP.text_auto_wrap 的默认值是 TX.isEnglishLang()，
                // 中文环境下为 false，正文会撞出框外。
                text_auto_wrap = true,
                lineSpacing = 1.2f,
                do_not_error_unknown_tag = true,
            });
            designer.Br();
        }

        /// <summary>英语语言族的 key，见 <c>GameStateAPI.CurrentLocale</c> 文档里列出的例子。</summary>
        const string EnglishFamily = "en";

        /// <summary>简体中文语言族的 key，见 <c>localization/___family_zh-cn.txt</c> 首行。</summary>
        const string ChineseFamily = "zh-cn";

        /// <summary>日文（默认）语言族的 key，见 <c>localization/___family__.txt</c>。</summary>
        const string JapaneseFamily = "_";

        /// <summary>
        /// 按语言族取字体，而不是用"当前语言"的字体——本页的语言由玩家在页面里自己选，
        /// 不一定和游戏此刻的语言设置一致，<c>TX.getDefaultFont()</c> 给的字体未必覆盖
        /// 玩家选中的这门语言的字符集（比如游戏当前是韩文，玩家在本页选了中文/日文）。
        /// <para>
        /// 非当前语言族的字体在这里第一次取时会触发一次字体包加载（<c>TXFamily.prepareLanguage</c>）。
        /// 这一页一辈子只弹一次、切换语言也最多来回点几次，这点开销可以接受；取不到就退回
        /// 当前语言的默认字体，宁可字形不理想也不能让整页建不出来。
        /// </para>
        /// </summary>
        static MFont ResolveFont(string family)
        {
            try
            {
                MFont font = TX.getFamilyByName(family)?.getDefaultFont();
                if (font != null)
                {
                    return font;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"[Polaris] 取语言族 {family} 的字体失败，改用当前语言字体：{e.Message}");
            }

            return TX.getDefaultFont();
        }

        // ================== 语言切换 ==================

        static void SwitchLanguage()
        {
            langIndex = (langIndex + 1) % Wordings.Length;
            Rebuild();
        }

        /// <summary>
        /// 换一种语言重建整页。不同语言的正文行数不同（<see cref="Wording.BodyH"/>），
        /// 沿用原来那套"先 Teardown 再 Build"最简单也最不容易出布局错位——不去尝试
        /// 原地替换某几个文本块的内容。
        /// </summary>
        static void Rebuild()
        {
            SceneTitleTemp scene = currentScene;
            float preservedFade = fade;

            Teardown();

            if (scene == null || !TryBuild(scene))
            {
                return;
            }

            // 不重新淡入：语言切换是玩家主动点出来的，此刻页面本来就是可见的，
            // 从头淡一遍反而像是重新弹出了一份新的告知。
            fade = preservedFade;
            designer.alpha = preservedFade;
        }

        // ================== 确认与收尾 ==================

        static void Confirm()
        {
            MarkAcknowledged();
            Teardown();
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

        static void MarkAcknowledged()
        {
            sessionAcknowledged = true;

            try
            {
                ConfigEntry<bool> entry = ResolveEntry();
                if (entry == null)
                {
                    return;
                }

                entry.Value = true;
                PolarisNoticeStore.File?.Save();
            }
            catch (Exception e)
            {
                // 落不了盘只影响"下次还弹一次"，不影响本局，所以不往上抛。
                Plugin.Logger.LogWarning($"[Polaris] 模组警示页的确认状态没能写入配置：{e.Message}");
            }
        }

        static ConfigEntry<bool> ResolveEntry()
        {
            if (configResolved)
            {
                return acknowledged;
            }

            configResolved = true;

            ConfigFile file = PolarisNoticeStore.File;
            if (file == null)
            {
                return null;
            }

            try
            {
                acknowledged = file.Bind(
                    ConfigSection, ConfigKey, false,
                    "玩家是否已经确认过标题画面的模组环境警示页。改回 false（或删掉本文件）会让它重新弹一次。");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris] 绑定模组警示页的确认状态失败：{e}");
                acknowledged = null;
            }

            return acknowledged;
        }
    }
}
