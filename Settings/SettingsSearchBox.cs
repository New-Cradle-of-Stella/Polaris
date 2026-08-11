using System;
using Polaris.Localization;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置界面底部那条搜索栏的内容：一个标签 + 一个输入框 + 一段状态文字。
    /// 标题画面与 ESC 菜单共用这一份实现，区别只在"这条栏画在哪个 designer 上"——
    /// 标题画面是 <see cref="SettingsSearchWindow"/> 自建的窗口，游戏内是原版游戏菜单的底部子区
    /// （见 <see cref="Patch.Patch_UiGMC_Constructor"/>）。
    /// <para>
    /// 输入走 <c>fnChangedDelay</c> 而不是 <c>fnChanged</c>，有两个原因：一是
    /// <c>fnChanged</c> 触发时 <c>LabeledInputField.text</c> 还没写成新值（原版是先跑回调再赋值），
    /// 拿到的是上一次的内容；二是它天然带防抖——把 <c>changed_delay_maxt</c> 调小到几帧，
    /// 连打时只在停手后过滤一次，而不是每敲一个字就重排一遍整页。
    /// </para>
    /// </summary>
    internal static class SettingsSearchBox
    {
        /// <summary>
        /// 搜索栏自身的高度。标题画面按它把原版设置面板缩短，游戏内按它反解子区的行高倍率
        /// （见 <see cref="SubareaRowScale"/>），两边看起来才是同一条栏。
        /// </summary>
        internal const float StripHeight = 42f;

        /// <summary>搜索栏与设置面板之间的留白。取 6 是为了和游戏菜单子区的 <c>margin_h</c> 对齐。</summary>
        internal const float StripGap = 6f;

        /// <summary>原版 <c>UiGameMenuTopTab</c> 的行高与行间距，用于反解 <see cref="SubareaRowScale"/>。</summary>
        const float SubareaRowHeight = 32f;
        const float SubareaMarginH = 6f;

        /// <summary>
        /// 游戏菜单底部子区的行高倍率。原版的换算是
        /// <c>cur_row_height = row_h * scale + margin_h * (scale - 1)</c>，
        /// 反解出让子区高度正好等于 <see cref="StripHeight"/> 的那个倍率。
        /// </summary>
        internal static float SubareaRowScale =>
            (StripHeight + SubareaMarginH) / (SubareaRowHeight + SubareaMarginH);

        /// <summary>控件在 designer 里的注册名。带 <c>plrs:</c> 前缀，理由同 <see cref="SettingDefinition.RowKey"/>。</summary>
        const string FieldName = "plrs:settings:search";

        const float LabelWidth = 58f;
        const float StatusWidth = 132f;
        const float FieldHeight = 24f;
        const float LabelSize = 14f;
        const float StatusSize = 12f;

        /// <summary>输入框再挤也不小于这个宽度；面板窄到放不下时宁可让状态文字被挤出去。</summary>
        const float MinFieldWidth = 120f;

        /// <summary>停手多少帧之后才真正过滤。8 帧 ≈ 0.13 秒，连打时不会每个字都重排一遍。</summary>
        const int ChangedDelay = 8;

        /// <summary>与设置界面其余文字同色（<c>UiCFG.P</c> 用的也是这个值）。</summary>
        const uint TextColor = 4283780170u;

        /// <summary>状态文字用浅一档的灰，和标签拉开层次；取值是原版置灰标签的那个色。</summary>
        const uint MutedColor = 4288057994u;

        static LabeledInputField field;
        static FillBlock status;

        /// <summary>
        /// 把搜索栏画进 <paramref name="box"/>。调用方负责保证这个 designer 已经 <c>init()</c> 过、
        /// 并且确实有东西可搜（一个模组都没注册过设置项时这条栏整个不出现）。
        /// </summary>
        internal static void Build(Designer box)
        {
            SettingsSearchStrings.Register();

            // 上一次画的那两个控件已经跟着旧 designer 一起没了，先松手再画，
            // 免得中途出异常时留下指向已销毁对象的引用。
            field = null;
            status = null;

            box.alignx = ALIGN.LEFT;

            // 必须在放下第一个块之前取：Designer.use_w 一旦当前行里有了内容，返回的就是
            // "这一行还剩多宽"而不是"框内有多宽"，放到下面去算会把标签的宽度扣两遍。
            float inner = box.use_w;

            box.addP(new DsnDataP(SettingsSearchStrings.Text(SettingsSearchStrings.Label), false)
            {
                name = "plrs_search_label",
                size = LabelSize,
                alignx = ALIGN.RIGHT,
                aligny = ALIGNY.MIDDLE,
                Col = MTRX.ColTrnsp,
                TxCol = C32.d2c(TextColor),
                swidth = LabelWidth,
                sheight = FieldHeight,
            });

            float width = Math.Max(MinFieldWidth, inner - LabelWidth - StatusWidth - StripGap * 2f);

            field = box.addInput(new DsnDataInput
            {
                name = FieldName,
                label = "",
                def = SettingsSearchFilter.Query,
                w = width,
                h = FieldHeight,
                size = (int)LabelSize,
                changed_delay_maxt = ChangedDelay,
                fnChangedDelay = fld =>
                {
                    SettingsSearchFilter.Apply(fld.text);
                    FineStatus();
                    return true;
                },
            });

            status = box.addP(new DsnDataP(StatusText(), false)
            {
                name = "plrs_search_status",
                size = StatusSize,
                alignx = ALIGN.LEFT,
                aligny = ALIGNY.MIDDLE,
                Col = MTRX.ColTrnsp,
                TxCol = C32.d2c(MutedColor),
                swidth = StatusWidth,
                sheight = FieldHeight,
            });

            box.Br();
        }

        /// <summary>
        /// 清空搜索并把所有行放回来。设置界面收起时调用——查询留到下次打开会让玩家对着
        /// 一份"缺了大半"的设置界面发懵，而搜索框那点内容不值得跨次保留。
        /// </summary>
        internal static void Reset()
        {
            // Unity 的假 null：控件可能已经随 designer 一起销毁了，必须用 != null 走重载。
            if (field != null)
            {
                // call_changed_delay: false——这里是"程序改的"，不该再绕一圈回调，
                // 下面已经直接把过滤撤销了。
                field.setValue("", call_changed_delay: false);
            }

            SettingsSearchFilter.Reset();
            FineStatus();
        }

        /// <summary>界面整个没了：松开对控件的引用，别让静态字段拖着已销毁的对象。</summary>
        internal static void Forget()
        {
            field = null;
            status = null;
        }

        static void FineStatus()
        {
            if (status != null)
            {
                status.text_content = StatusText();
            }
        }

        /// <summary>输入框右边那句话：没输入时是提示，有输入时是命中条数。</summary>
        static string StatusText()
        {
            if (SettingsSearchFilter.Query.Length == 0)
            {
                return SettingsSearchStrings.Text(SettingsSearchStrings.Hint);
            }

            if (SettingsSearchFilter.MatchCount == 0)
            {
                return SettingsSearchStrings.Text(SettingsSearchStrings.NoResult);
            }

            return string.Format(
                SettingsSearchStrings.Text(SettingsSearchStrings.Result), SettingsSearchFilter.MatchCount);
        }
    }
}
