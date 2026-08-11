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
    /// 全屏暗底、竖直居中的正文，下方一个"打开官方规则页"按钮加一个确认按钮，再加一行按键提示；
    /// 语言切换的两个按钮单独钉在屏幕右下角。内容分两段：
    /// 先告诉玩家这份游戏跑在模组环境下、出了问题先自己排查别拿去找游戏原作者，再是与官方的
    /// 关系界定（非商业、社区自制、与官方无隶属、模组责任归各自作者）＋官方规则页地址。
    /// <para>
    /// 后一段是硬要求，不是可有可无的免责话术：Polaris 得以公开发布的前提就是遵守官方那份
    /// 《Game Program Modifying &amp; Mod Creation Limitation》（见
    /// <see cref="PolarisMeta.ModGuidelinesUrl"/>），其中明确要求"必须写明本框架为社区自制、
    /// 与官方无关，且用它做出的 MOD 由各自作者负责"，以及"必须写明使用 MOD 可能引发异常且
    /// 官方不提供支持"。删改这两段之前请先回去核对那一页的最新版本。
    /// </para>
    /// <para>
    /// 一次只显示一种语言，不跟随游戏语言设置——默认英语，玩家可以点右下角那两个语言按钮
    /// （左＝上一种、右＝下一种）、或者按左右方向键在英/中/日之间循环切换。之所以不跟随游戏当前语言：这一页的责任声明必须对所有玩家
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

        const float LangButtonW = 90f;
        const float LangButtonH = 26f;
        const float LinkButtonW = 340f;
        const float LinkButtonH = 30f;
        const float ConfirmButtonW = 400f;
        const float ButtonH = 38f;

        /// <summary>
        /// 各行之间统一的垂直间距。
        /// <para>
        /// 只有这一个值、而不是"每行各自一个间距"，是因为 <c>item_margin_y_px</c> 根本做不到后者：
        /// 它的 setter 写的是 <c>DesignerRowMem.margin_y_px</c> 这个单值字段，而
        /// <c>DesignerRowMem.Remake()</c>（<c>WH()</c> 改过尺寸后由 <c>activate()</c> 触发）会拿
        /// <b>当时</b>那一个值把所有行重排一遍——加某一行之前临时调大再改回去，重排时会被一起抹平。
        /// 想要逐行不同的间距只能自己插占位块，不值得。
        /// </para>
        /// <para>
        /// 22 这个值由两个按钮定：<c>aBtnNel</c> 的皮肤装饰会溢出按钮矩形十几像素，间距太小时
        /// 上下两个按钮的装饰会叠在一起。文本行之间用同一个值看着也正常。
        /// </para>
        /// </summary>
        const float RowGapY = 22f;

        /// <summary>语言按钮离屏幕右边缘的距离。</summary>
        const float LangMarginX = 32f;

        /// <summary>
        /// "上一种语言""下一种语言"两个按钮之间的水平间距。和 <see cref="RowGapY"/> 同理，
        /// 留这么宽是因为 <c>aBtnNel</c> 的皮肤装饰会溢出按钮矩形，挨太近会互相压住。
        /// </summary>
        const float LangButtonGapX = 20f;

        /// <summary>
        /// 语言按钮离屏幕下边缘的距离。原版标题画面自己那排语言按钮贴着屏幕底边，本页显示期间
        /// 已由 <see cref="TitleChrome"/> 压掉，不会再互相干扰；这里仍然抬到 64 而不是贴边，
        /// 是因为按钮贴着视口边缘本身就不好点，且非 16:9 的窗口比例下更容易被裁到。
        /// </summary>
        const float LangMarginY = 64f;

        const float HeadingSize = 20f;
        const float HintSize = 13f;

        /// <summary>
        /// 声明块的字号。比正文小半档：正文是"出了问题该怎么办"的操作指引，声明块是
        /// 与官方的关系界定＋规则页地址，玩家不需要逐字读也该一眼看出它在那儿。
        /// </summary>
        const float NoticeSize = 13.5f;

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
        static readonly Color32 NoticeColor = new Color32(196, 196, 196, 255);

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

        /// <summary>
        /// 官方规则页地址，取自 <see cref="PolarisMeta.ModGuidelinesUrl"/>。三段声明文案都把它
        /// 单独放在最后一行——一行 80 来个半角字符，在 <see cref="ContentW"/> 的宽度下不会被
        /// 自动换行拆开，玩家可以照着抄下来。
        /// </summary>
        const string GuidelinesUrl = PolarisMeta.ModGuidelinesUrl;

        /// <summary>
        /// 一种语言的完整页面文案 + 排版参数 + 取字体用的语言族 key。
        /// <para>
        /// 刻意不带任何"这一段多高"的参数：文本块的高度由 <c>FillBlock.get_sheight_px()</c> 自己
        /// 按实测文本高度给出（传进去的 <c>DsnDataP.sheight</c> 只是下限，见
        /// <see cref="AddParagraph"/>），三种语言的行数不同也不必各配一套数字。
        /// </para>
        /// </summary>
        readonly struct Wording
        {
            public Wording(string heading, string body, float bodySize,
                string notice, string linkLabel,
                string confirmLabel, string selfLabel, string hint, string fontFamily)
            {
                Heading = heading;
                Body = body;
                BodySize = bodySize;
                Notice = notice;
                LinkLabel = linkLabel;
                ConfirmLabel = confirmLabel;
                SelfLabel = selfLabel;
                Hint = hint;
                FontFamily = fontFamily;
            }

            public string Heading { get; }
            public string Body { get; }
            public float BodySize { get; }

            /// <summary>
            /// 与官方的关系界定：非商业、社区自制、与官方无隶属、模组责任归各自作者，
            /// 末行是官方规则页地址。这一段是遵守该规则页的一部分，不是可选的装饰文字。
            /// </summary>
            public string Notice { get; }

            /// <summary>打开官方规则页那个按钮上的文字，见 <see cref="OpenGuidelines"/>。</summary>
            public string LinkLabel { get; }
            public string ConfirmLabel { get; }

            /// <summary>
            /// 这门语言自己的名字。两个语言按钮上写的是"切过去会变成哪门语言"，标题由
            /// <see cref="PrevLabel"/> / <see cref="NextLabel"/> 从相邻那份文案上取——
            /// 不在每份文案里各写一遍"上一个是谁、下一个是谁"，那样加一门语言就要改三处。
            /// </summary>
            public string SelfLabel { get; }
            public string Hint { get; }
            public string FontFamily { get; }
        }

        static readonly Wording EnglishWording = new Wording(
            "MODDED GAME NOTICE",
            "This copy is modded, so it no longer behaves like the original. A crash, a freeze, a broken save or anything else odd is not automatically a bug in the base game.\n" +
            "Check it yourself first: turn the suspect mods off from the Polaris page and restart; if it still happens with every mod disabled, confirm it once on a clean, unmodded copy. Until then, do not report it to the game's original author or the official channels. Report confirmed mod issues to that mod's author.\n" +
            "If Polaris itself produces an error report, please submit that to " + ReportTarget + ".",
            15f,
            "Polaris is a non-commercial, community-created framework. It is not an official product, and it carries no affiliation with, endorsement by, or support from NanameHacha. It is published with the game author's permission, on the condition that it follows the official mod-creation guidelines; that permission is not an endorsement. Mods built on Polaris are the sole responsibility of their own authors.\n" +
            GuidelinesUrl,
            "OPEN THE GUIDELINES PAGE",
            "I UNDERSTAND", "English",
            $"{KeyHint.Left}{KeyHint.Right} language    {KeyHint.Submit} confirm", EnglishFamily);

        static readonly Wording ChineseWording = new Wording(
            "模组环境提示",
            "你的游戏装了模组，运行结果和原版并不一致。崩溃、卡死、存档损坏或任何奇怪的表现，都不能默认是游戏本体的问题。\n" +
            "请先自己排查：在标题画面的 Polaris 页里关掉可疑的模组，重启看问题是否还在；全部模组都关掉后仍然复现，再用一份干净的游戏本体确认一次。在这之前请不要把问题反馈给游戏原作者或官方渠道；确认是某个模组导致的，请反馈给该模组的作者。\n" +
            "如果 Polaris 自己弹出了错误报告，请把它提交到 " + ReportTarget + "。",
            16f,
            "Polaris 是非商业的社区自制框架，并非官方产品，与 NanameHacha 没有任何隶属关系，也未获得其背书或技术支持。本框架是在遵守官方模组创作规则的前提下、经游戏作者许可公开发布的；许可不等于官方认可。使用 Polaris 制作的模组，责任完全由各自的模组作者承担。\n" +
            GuidelinesUrl,
            "打开官方规则页",
            "我已了解", "中文",
            $"{KeyHint.Left}{KeyHint.Right} 切换语言    {KeyHint.Submit} 确认", ChineseFamily);

        static readonly Wording JapaneseWording = new Wording(
            "MOD環境について",
            "このゲームにはMODが導入されており、挙動はオリジナルと同じではありません。クラッシュ・フリーズ・セーブデータの破損など、おかしな症状がゲーム本体の不具合とは限りません。\n" +
            "まずご自身で切り分けてください：タイトル画面の Polaris ページで疑わしいMODを無効化して再起動し、すべてのMODを無効にしても再現する場合は、MODを一切入れていない状態でもう一度確認してください。それまではゲームの原作者や公式の窓口へ報告しないでください。MODが原因と判明した場合は、そのMODの作者へご報告ください。\n" +
            "Polaris 自体がエラーレポートを出力した場合は、" + ReportTarget + " へご提出ください。",
            15.5f,
            "Polaris は非営利のコミュニティ制作フレームワークであり、公式の製品ではありません。NanameHacha とは一切関係がなく、公認およびサポートも受けていません。公式のMOD作成規約を遵守することを条件に、ゲーム作者の許可を得て公開されています（許可は公認を意味するものではありません）。Polaris を用いて制作されたMODの責任は、それぞれのMOD作者にあります。\n" +
            GuidelinesUrl,
            "規約ページを開く",
            "了解しました", "日本語",
            $"{KeyHint.Left}{KeyHint.Right} 言語切替    {KeyHint.Submit} 決定", JapaneseFamily);

        /// <summary>
        /// 循环顺序：英 → 中 → 日 → 英……<see cref="langIndex"/> 默认 0，也就是默认英语。
        /// </summary>
        static readonly Wording[] Wordings = [EnglishWording, ChineseWording, JapaneseWording];

        static int langIndex;

        static Wording Current => Wordings[langIndex];

        /// <summary>右边那个按钮的标题：往前切一格是哪门语言。</summary>
        static string NextLabel => Wordings[Wrap(langIndex + 1)].SelfLabel;

        /// <summary>左边那个按钮的标题：往后切一格是哪门语言。</summary>
        static string PrevLabel => Wordings[Wrap(langIndex - 1)].SelfLabel;

        static int Wrap(int index) => (index + Wordings.Length) % Wordings.Length;

        // ================== 状态 ==================

        static GameObject host;
        static Designer designer;
        static float fade;

        /// <summary>
        /// 两个语言切换按钮共用的宿主与 Designer。它被钉在屏幕右下角，不参与正文那一列的排版，
        /// 所以必须是独立的一份——同一个 Designer 里的块只能顺着 <c>DesignerRow</c> 往下排。
        /// </summary>
        static GameObject langHost;

        static Designer langDesigner;

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

        /// <summary>
        /// 推进淡入动画，并读一次左右方向键；由 <see cref="Patch.Patch_SceneTitleTemp_runIRD"/>
        /// 每帧调用。方向键也在这里读，是因为本页需要的每帧时机只有这一个，
        /// 不值得为它再往 <see cref="ITitleOverlay"/> 上加一个钩子。
        /// </summary>
        internal static void AdvanceFade(float deltaSeconds)
        {
            if (designer == null)
            {
                return;
            }

            if (fade < 1f)
            {
                fade = Mathf.Min(1f, fade + deltaSeconds / FadeSeconds);
                ApplyAlpha(fade);
            }

            PollLanguageKeys();
        }

        /// <summary>
        /// 左右方向键切换语言。
        /// <para>
        /// 语言按钮被挪到屏幕右下角之后成了独立 Designer，也就有了自己的 <c>BtnContainer</c>：
        /// 选中态（<c>aBtn.PreSelected</c>）是全局的，但方向键找邻居是在同一个容器内按几何算的，
        /// 键盘/手柄玩家走不到角上那两个按钮。所以这里把左右键固定绑成切换语言，提示行里也写明了；
        /// 正文那一列每行只有一个块，左右键本来也没有别的用处。
        /// </para>
        /// </summary>
        static void PollLanguageKeys()
        {
            if (IN.isRP())
            {
                SwitchLanguage(1);
            }
            else if (IN.isLP())
            {
                SwitchLanguage(-1);
            }
        }

        /// <summary>两个 Designer 一起淡入。</summary>
        static void ApplyAlpha(float alpha)
        {
            designer.alpha = alpha;

            if (langDesigner != null)
            {
                langDesigner.alpha = alpha;
            }
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
                Plugin.Logger.LogError($"[Polaris] Failed to build the mod warning page; skipped this session: {e}");
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
            designer.alignx = ALIGN.CENTER;
            designer.item_margin_y_px = RowGapY;

            // 先按"内容贴着内区顶边"排一遍：块高度是文本实测出来的，排完才知道整页多高，
            // 竖直居中要用的 margin_in_tb 只能等排完再算（见本方法末尾的 CenterVertically）。
            designer.margin_in_tb = 0f;
            designer.init();

            MFont font = ResolveFont(w.FontFamily);

            AddParagraph(w.Heading, HeadingSize, HeadingColor, font, border: true);
            AddParagraph(w.Body, w.BodySize, BodyColor, font, border: false);

            // 声明块紧跟正文、排在确认按钮之前：玩家点"我已了解"之前必须先看见它。
            AddParagraph(w.Notice, NoticeSize, NoticeColor, font, border: false);

            // 规则页做成按钮，而不是把声明块末行那个网址变成可点的富文本：游戏的文本标签只有
            // align/b/bmc/bmcs/fiximg/font/i/img/key/key_s/rb/s/shape 这些（见 unsafeAssem 里
            // TextRendererHtmlTag.TagNameIs 的全部调用点），没有链接类标签，文本块本身也不做
            // 命中测试——整个游戏里连一次 Application.OpenURL 都没有。网址仍然照原样印在声明块里，
            // 这个按钮只是省掉手抄。
            designer.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "polaris_warning_guidelines",
                skin = "normal_dark",
                title = w.LinkLabel,
                w = LinkButtonW,
                h = LinkButtonH,
                fnClick = _ =>
                {
                    OpenGuidelines();
                    return true;
                }
            });
            designer.Br();

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

            AddParagraph(w.Hint, HintSize, HintColor, font, border: true, html: true);

            CenterVertically(screenH);

            // 角上的语言按钮先建、再 Select 确认按钮：选中态 aBtn.PreSelected 是全局的，
            // 后建的按钮有可能把它抢过去，那样一进页面按下确定键就成了切换语言。
            BuildLangToggle(scene);

            designer.activate();
            confirm.Select();

            // alpha 必须在所有块都加完之后再设：setter 是遍历当前已有的块逐个下发的，
            // 先设后加的块拿不到这个值，会以 alpha=1 直接跳出来。
            fade = 0f;
            ApplyAlpha(0f);

            // 玩家在 logo 淡入期间按下确定键也会立刻触发本页（原版闸门里的 IN.kettei3()），
            // 那一下按键此刻还没消费掉，不清掉的话确认按钮会在同一帧被这次按下直接点掉。
            IN.clearPushDown(strong: true);
        }

        /// <summary>
        /// 把已经排完版的内容整体挪到屏幕竖直中央。
        /// <para>
        /// <c>Designer</c> 的内容永远从内区顶边往下排（<c>fineRow()</c> 里
        /// <c>Row.BasePx(-inw / 2, inh / 2)</c>），竖直居中只能靠上下内边距把内区收窄到内容那么高。
        /// 而内容多高要等文本块实测完才知道，所以这里是"排完再收边距、然后 <c>init()</c> 重新落位"。
        /// <c>maxh_pixel</c> 就是排完之后的行内容总高。
        /// </para>
        /// <para>
        /// 之所以敢在这之后让 <c>activate()</c> 去触发一次 <c>Remake()</c>：本页所有行用的是同一个
        /// <see cref="RowGapY"/>，重排出来的总高和这里量到的完全一致，居中不会因为重排而错位。
        /// </para>
        /// </summary>
        static void CenterVertically(float screenH)
        {
            float contentH = designer.maxh_pixel;

            designer.margin_in_tb = Mathf.Max(0f, (screenH - contentH) / 2f);
            designer.init();
        }

        /// <summary>
        /// 加一段居中文本。<c>sheight</c> 传 0：<c>FillBlock.get_sheight_px()</c> 取的是
        /// "实测文本高" 与 <c>heightPixel</c> 里的较大值，也就是说传进去的高度只是下限——
        /// 给 0 就让块自己贴合文本，不同语言的行数差异不必再各配一套数字，中间也不会留下大片空白。
        /// </summary>
        static void AddParagraph(
            string text, float size, Color32 color, MFont font, bool border, bool html = false)
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
                sheight = 0f,
                // 显式写死：DsnDataP.text_auto_wrap 的默认值是 TX.isEnglishLang()，
                // 中文环境下为 false，正文会撞出框外。
                text_auto_wrap = true,
                lineSpacing = 1.2f,
                do_not_error_unknown_tag = true,
            });
            designer.Br();
        }

        /// <summary>
        /// 语言切换按钮：独立一个 Designer，钉在屏幕右下角，左"上一种语言"、右"下一种语言"，
        /// 两个按钮上写的都是切过去之后会变成哪门语言。
        /// <para>
        /// 位置用 <c>IN.PosP</c> 按像素给（内部按 <c>IN.ppu = 64</c> 换算成世界单位），
        /// 宿主的局部原点就是屏幕中心——整页底板正是以它为中心铺满全屏的。Designer 自身也以
        /// 中心定位，所以要再减去半个按钮。
        /// </para>
        /// <para>
        /// 位置在建页时按当时的 <c>IN.wh / IN.hh</c> 算死。建完之后改窗口尺寸会偏——但那种情况下
        /// 整页底板的尺寸同样是过期的，本页本来就不跟随窗口变化，这里不额外处理。
        /// </para>
        /// </summary>
        static void BuildLangToggle(SceneTitleTemp scene)
        {
            // 两个按钮共一行，Designer 必须够宽：DesignerRow.Add 一旦发现这一行放不下就会自动
            // 换行（bounds_w_px 来自 inw），宽度按一个按钮给的话两个按钮会上下叠起来。
            float rowW = LangButtonW * 2f + LangButtonGapX;

            langHost = IN.CreateGob(scene.gameObject, "-polaris_mod_warning_lang");

            langDesigner = langHost.AddComponent<Designer>();
            langDesigner.Smallest();
            langDesigner.WH(rowW, LangButtonH);
            langDesigner.alignx = ALIGN.CENTER;
            langDesigner.item_margin_x_px = LangButtonGapX;
            langDesigner.init();

            // 不 Br()：两个按钮留在同一行里，先加的在左边。
            AddLangButton("polaris_warning_lang_prev", PrevLabel, -1);
            AddLangButton("polaris_warning_lang_next", NextLabel, 1);

            langDesigner.activate();

            IN.PosP(
                langHost.transform,
                IN.wh - LangMarginX - rowW / 2f,
                0f - IN.hh + LangMarginY + LangButtonH / 2f,
                OverlayZ);
        }

        static void AddLangButton(string name, string title, int step)
        {
            langDesigner.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = name,
                skin = "normal_dark",
                title = title,
                w = LangButtonW,
                h = LangButtonH,
                fnClick = _ =>
                {
                    SwitchLanguage(step);
                    return true;
                }
            });
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
                Plugin.Logger.LogWarning($"[Polaris] Failed to get the font for language family {family}; falling back to the current language font: {e.Message}");
            }

            return TX.getDefaultFont();
        }

        // ================== 打开规则页 ==================

        /// <summary>
        /// 交给系统默认浏览器打开官方规则页。
        /// <para>
        /// 全屏独占的时候浏览器可能被压在游戏窗口后面（玩家 Alt+Tab 才看得到），所以声明块末行
        /// 那个网址一直保留着——按钮只是省掉手抄，不是唯一的路。刻意不动系统剪贴板：玩家没让
        /// Polaris 覆盖剪贴板内容，点一下按钮就悄悄清掉他原本复制的东西不合适。
        /// </para>
        /// </summary>
        static void OpenGuidelines()
        {
            try
            {
                Application.OpenURL(GuidelinesUrl);
            }
            catch (Exception e)
            {
                // 打不开浏览器不影响本页的任何其它功能，记一条日志就够——网址还印在页面上。
                Plugin.Logger.LogWarning($"[Polaris] Failed to open the official rules page: {e.Message}");
            }
        }

        // ================== 语言切换 ==================

        /// <summary>
        /// 切到下一种（<paramref name="step"/> 为 1）或上一种（-1）语言。角上那两个按钮各走一个方向，
        /// 左右方向键与它们一一对应。
        /// </summary>
        static void SwitchLanguage(int step)
        {
            langIndex = Wrap(langIndex + step);
            Rebuild();
        }

        /// <summary>
        /// 换一种语言重建整页。不同语言的行数不同、整页高度跟着变，沿用原来那套
        /// "先 Teardown 再 Build"最简单也最不容易出布局错位——不去尝试原地替换某几个文本块的内容。
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
            ApplyAlpha(preservedFade);
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
            langDesigner = null;
            fade = 0f;

            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
                host = null;
            }

            if (langHost != null)
            {
                UnityEngine.Object.Destroy(langHost);
                langHost = null;
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
                Plugin.Logger.LogWarning($"[Polaris] Could not write the acknowledged state of the mod warning page to config: {e.Message}");
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
                    "Whether the player has acknowledged the mod environment warning page on the title screen. Setting it back to false (or deleting this file) makes it appear again.");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris] Failed to bind the acknowledged state of the mod warning page: {e}");
                acknowledged = null;
            }

            return acknowledged;
        }
    }
}
