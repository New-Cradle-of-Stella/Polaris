using System;
using BepInEx.Configuration;
using nel;
using nel.title;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>
    /// 一次性的模组环境警示页：全屏正文告知模组环境注意事项与官方关系界定（含官方规则页地址），
    /// 语言默认英语、可用方向键或右下角按钮切换。借用原版闸门接管标题按钮行的激活。
    /// 确认状态落在 <c>_polaris_notice.cfg</c>，确认后永不再弹；语言选择不落盘。
    /// </summary>
    internal static class PolarisModWarning
    {
        /// <summary>供 <see cref="TitleOverlays"/> 调用的适配器：本类全静态，用不持有状态的适配器转发接口方法。</summary>
        internal static readonly ITitleOverlay Overlay = new TitleOverlay(Gate, AdvanceFade);

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

        /// <summary>各行之间统一的垂直间距；只能全局设一个值（<c>item_margin_y_px</c> 的重排机制所限），22 是为避免按钮皮肤装饰溢出叠加而定。</summary>
        const float RowGapY = 22f;

        /// <summary>语言按钮离屏幕右边缘的距离。</summary>
        const float LangMarginX = 32f;

        /// <summary>语言切换两按钮间的水平间距；留宽是为避免按钮皮肤装饰互相压住。</summary>
        const float LangButtonGapX = 20f;

        /// <summary>语言按钮离屏幕下边缘的距离；抬高避免贴边不好点、非 16:9 比例下被裁切。</summary>
        const float LangMarginY = 64f;

        const float HeadingSize = 20f;
        const float HintSize = 13f;

        /// <summary>声明块字号，比正文小半档，因为它是不必逐字读的辅助信息。</summary>
        const float NoticeSize = 13.5f;

        /// <summary>整页在标题场景里的 z；-3 稳稳盖住标题画面的一切，不越过按键设置（-4.25）。</summary>
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

        /// <summary>Polaris 自身错误报告的提交去处，与错误报告文件结尾共用同一常量。</summary>
        const string ReportTarget = PolarisMeta.ReportTarget;

        /// <summary>官方规则页地址；单独放在每段声明的最后一行，避免被自动换行拆开。</summary>
        const string GuidelinesUrl = PolarisMeta.ModGuidelinesUrl;

        /// <summary>一种语言的完整页面文案 + 排版参数 + 取字体用的语言族 key；不带高度参数，文本块按实测高度自适应。</summary>
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

            /// <summary>与官方的关系界定声明（末行是规则页地址），是遵守规则的硬性内容，非装饰文字。</summary>
            public string Notice { get; }

            /// <summary>打开官方规则页那个按钮上的文字，见 <see cref="OpenGuidelines"/>。</summary>
            public string LinkLabel { get; }
            public string ConfirmLabel { get; }

            /// <summary>这门语言自己的名字；<see cref="PrevLabel"/>/<see cref="NextLabel"/> 从相邻文案取用，避免每份文案互相硬编码。</summary>
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
            "Polaris has no plans, now or in the future, to be made compatible with any other mod framework. Forcing such compatibility is done entirely at your own risk.\n" +
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
            "Polaris 今后不会有任何与其他模组框架兼容的计划。如果强行使其兼容，后果自负。\n" +
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
            "Polaris は今後、他のMODフレームワークとの互換性を持たせる予定は一切ありません。無理に互換させた場合の結果はすべて自己責任となります。\n" +
            GuidelinesUrl,
            "規約ページを開く",
            "了解しました", "日本語",
            $"{KeyHint.Left}{KeyHint.Right} 言語切替    {KeyHint.Submit} 決定", JapaneseFamily);

        /// <summary>循环顺序：英 → 中 → 日 → 英……默认索引 0（英语）。</summary>
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

        /// <summary>两个语言切换按钮共用的宿主与 Designer；钉在屏幕右下角，独立于正文排版。</summary>
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

                // 配置读不出来时按"没确认过"处理：宁可多弹一次，也不要把这页永远吞掉。
                return ResolveEntry()?.Value != true;
            }
        }

        /// <summary>每帧从原版闸门问过来一次：返回 true 表示本页仍要拦住标题菜单。首次调用时建页。</summary>
        internal static bool Gate(SceneTitleTemp scene)
        {
            if (!IsPending)
            {
                return false;
            }

            // designer 是 UnityEngine.Object：场景重建后旧实例已销毁，会如实返回 null 触发重建。
            if (designer == null && !TryBuild(scene))
            {
                // 建不出来就放行，锁死玩家比少看一页提示严重得多。
                buildFailed = true;
                return false;
            }

            return true;
        }

        /// <summary>推进淡入动画并读一次左右方向键（复用同一个每帧时机，不为此单独加钩子）。</summary>
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

        /// <summary>左右方向键切换语言：语言按钮是独立 Designer，键盘/手柄导航走不到那里，故固定绑定左右键。</summary>
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

            // 挂在标题场景对象下面：场景卸载时跟着销毁；CreateGob 继承 layer/tag，UI 才能被 GUI 相机拍到。
            host = IN.CreateGob(scene.gameObject, "-polaris_mod_warning");
            IN.setZ(host.transform, OverlayZ);

            designer = host.AddComponent<Designer>();

            // Smallest() 清掉圆角、行间距和出现动画缩放，下面只写回真正需要的项。
            designer.Smallest();
            designer.WH(screenW, screenH);
            designer.bgcol = C32.d2c(BackdropColor);
            designer.margin_in_lr = (screenW - contentW) / 2f;
            designer.alignx = ALIGN.CENTER;
            designer.item_margin_y_px = RowGapY;

            // 先贴顶边排一遍，块高度取决于实测文本，居中边距要等排完才能算（见 CenterVertically）。
            designer.margin_in_tb = 0f;
            designer.init();

            MFont font = ResolveFont(w.FontFamily);

            AddParagraph(w.Heading, HeadingSize, HeadingColor, font, border: true);
            AddParagraph(w.Body, w.BodySize, BodyColor, font, border: false);

            // 声明块紧跟正文、排在确认按钮之前：玩家确认前必须先看见它。
            AddParagraph(w.Notice, NoticeSize, NoticeColor, font, border: false);

            // 规则页做成按钮而非可点富文本：游戏文本标签不支持链接类，网址仍原样印在声明块里作兜底。
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
                skin = "normal_dark", // 与原版那一页的按钮皮肤一致
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

            // 角上的语言按钮先建、再 Select 确认按钮：否则后建的按钮可能抢走全局选中态。
            BuildLangToggle(scene);

            designer.activate();
            confirm.Select();

            // alpha 必须在所有块加完之后再设，否则先设后加的块拿不到这个值。
            fade = 0f;
            ApplyAlpha(0f);

            // 清掉尚未消费的确定键，否则确认按钮会在同一帧被这次按下直接点掉。
            IN.clearPushDown(strong: true);
        }

        /// <summary>把已排完版的内容整体挪到屏幕竖直中央，靠收窄上下内边距实现（内容高度需先实测才知道）。</summary>
        static void CenterVertically(float screenH)
        {
            float contentH = designer.maxh_pixel;

            designer.margin_in_tb = Mathf.Max(0f, (screenH - contentH) / 2f);
            designer.init();
        }

        /// <summary>加一段居中文本；sheight 传 0 让块按实测文本高度自适应，避免为不同语言各配一套数字。</summary>
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
                // 显式写死：默认值在中文环境下为 false，会导致正文撞出框外。
                text_auto_wrap = true,
                lineSpacing = 1.2f,
                do_not_error_unknown_tag = true,
            });
            designer.Br();
        }

        /// <summary>语言切换按钮：独立 Designer，钉在屏幕右下角；位置按建页时的窗口尺寸算死，不跟随窗口变化。</summary>
        static void BuildLangToggle(SceneTitleTemp scene)
        {
            // 两个按钮共一行，Designer 必须够宽，否则会自动换行叠起来。
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

        /// <summary>英语语言族的 key，见 <c>PolarisAPI.Game.Localization.CurrentLocale</c> 文档里列出的例子。</summary>
        const string EnglishFamily = "en";

        /// <summary>简体中文语言族的 key，见 <c>localization/___family_zh-cn.txt</c> 首行。</summary>
        const string ChineseFamily = "zh-cn";

        /// <summary>日文（默认）语言族的 key，见 <c>localization/___family__.txt</c>。</summary>
        const string JapaneseFamily = "_";

        /// <summary>按语言族取字体（而非当前游戏语言），因为本页语言由玩家自选，可能与游戏当前语言不一致；取不到则退回当前语言默认字体。</summary>
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

        /// <summary>交给系统默认浏览器打开官方规则页；刻意不动剪贴板，避免悄悄覆盖玩家原本复制的内容。</summary>
        static void OpenGuidelines()
        {
            try
            {
                Application.OpenURL(GuidelinesUrl);
            }
            catch (Exception e)
            {
                // 打不开浏览器不影响本页其它功能，记日志即可，网址还印在页面上。
                Plugin.Logger.LogWarning($"[Polaris] Failed to open the official rules page: {e.Message}");
            }
        }

        // ================== 语言切换 ==================

        /// <summary>切到下一种（step=1）或上一种（-1）语言。</summary>
        static void SwitchLanguage(int step)
        {
            langIndex = Wrap(langIndex + step);
            Rebuild();
        }

        /// <summary>换一种语言重建整页（先 Teardown 再 Build），因为各语言行数不同、整页高度会变。</summary>
        static void Rebuild()
        {
            SceneTitleTemp scene = currentScene;
            float preservedFade = fade;

            Teardown();

            if (scene == null || !TryBuild(scene))
            {
                return;
            }

            // 不重新淡入：页面已可见，从头淡一遍反而像重新弹出了新的告知。
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
                // 落不了盘只影响下次是否还弹一次，不影响本局。
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
