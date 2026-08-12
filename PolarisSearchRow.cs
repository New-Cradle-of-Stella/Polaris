using System;
using Polaris.Localization;
using XX;

namespace Polaris
{
    /// <summary>
    /// 一行搜索栏：标签 + 输入框 + 右侧状态文字，被设置界面与模组管理页共用同一份实现。
    /// 输入走 <c>fnChangedDelay</c>（带防抖，且拿到的是最新值而非 fnChanged 的旧值）。
    /// 过滤回调里不要重建所在 designer，会把正在输入的控件销毁；只应就地拨块显隐（见 <see cref="SetVisible"/>）。
    /// </summary>
    internal sealed class PolarisSearchRow
    {
        const float LabelWidth = 58f;
        const float StatusWidth = 132f;
        const float FieldHeight = 24f;
        const float LabelSize = 14f;
        const float StatusSize = 12f;

        /// <summary>标签与输入框、输入框与状态文字之间的留白。</summary>
        const float Gap = 6f;

        /// <summary>输入框再挤也不小于这个宽度；框窄到放不下时宁可让状态文字被挤出去。</summary>
        const float MinFieldWidth = 120f;

        /// <summary>停手多少帧之后才真正过滤。8 帧 ≈ 0.13 秒，连打时不会每个字都重排一遍。</summary>
        const int ChangedDelay = 8;

        /// <summary>与界面其余文字同色（原版 <c>UiCFG.P</c> 用的也是这个值）。</summary>
        const uint TextColor = 4283780170u;

        /// <summary>状态文字用浅一档的灰，和标签拉开层次；取值是原版置灰标签的那个色。</summary>
        const uint MutedColor = 4288057994u;

        readonly string name;
        readonly string hintKey;
        readonly Func<string, int> onQuery;

        LabeledInputField field;
        FillBlock status;
        int matchCount;

        /// <param name="name">控件在 designer 里的注册名，带 <c>plrs:</c> 前缀免得撞上原版的检索名。</param>
        /// <param name="hintKey">框空着时右侧显示的提示语，用 <see cref="SearchStrings"/> 上的常量。</param>
        /// <param name="onQuery">查询变化时调用，返回命中条数（用于状态文字）；真正的过滤由它负责。</param>
        internal PolarisSearchRow(string name, string hintKey, Func<string, int> onQuery)
        {
            this.name = name;
            this.hintKey = hintKey;
            this.onQuery = onQuery;
        }

        /// <summary>当前查询串（原始输入，未切词）。空串表示没有过滤。</summary>
        internal string Query { get; private set; } = "";

        /// <summary>把搜索栏画进 <paramref name="box"/>（须已 <c>init()</c> 过）；重建界面时重画即可，<see cref="Query"/> 会带入新输入框。</summary>
        internal void Build(Designer box)
        {
            SearchStrings.Register();

            // 上一次的控件已跟着旧 designer 没了，先松手再画，避免中途异常留下悬空引用。
            field = null;
            status = null;

            box.alignx = ALIGN.LEFT;

            // 须在放下第一个块之前取：一旦当前行有内容，use_w 返回的是剩余宽度而非框内总宽。
            float inner = box.use_w;

            box.addP(new DsnDataP(SearchStrings.Text(SearchStrings.Label), false)
            {
                name = name + ":label",
                size = LabelSize,
                alignx = ALIGN.RIGHT,
                aligny = ALIGNY.MIDDLE,
                Col = MTRX.ColTrnsp,
                TxCol = C32.d2c(TextColor),
                swidth = LabelWidth,
                sheight = FieldHeight,
            });

            float width = Math.Max(MinFieldWidth, inner - LabelWidth - StatusWidth - Gap * 2f);

            field = box.addInput(new DsnDataInput
            {
                name = name,
                label = "",
                def = Query,
                w = width,
                h = FieldHeight,
                size = (int)LabelSize,
                changed_delay_maxt = ChangedDelay,
                fnChangedDelay = fld =>
                {
                    Apply(fld.text);
                    return true;
                },
            });

            status = box.addP(new DsnDataP(StatusText(), false)
            {
                name = name + ":status",
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

        /// <summary>按 <paramref name="query"/> 过滤并刷新状态文字。</summary>
        internal void Apply(string query)
        {
            Query = query ?? "";
            matchCount = onQuery(Query);
            FineStatus();
        }

        /// <summary>清空搜索并把所有行放回来；界面收起时调用，避免下次打开对着半过滤的列表发懵。</summary>
        internal void Reset()
        {
            if (Query.Length == 0)
            {
                return;
            }

            // Unity 的假 null：控件可能已随 designer 销毁，须用 != null 走重载判断。
            if (field != null)
            {
                // call_changed_delay: false——程序改的值不必再绕一圈回调，Apply 会直接撤销过滤。
                field.setValue("", call_changed_delay: false);
            }

            Apply("");
        }

        /// <summary>界面整个没了：松开对控件的引用，别让字段拖着已销毁的对象。</summary>
        internal void Forget()
        {
            field = null;
            status = null;
        }

        void FineStatus()
        {
            if (status != null)
            {
                status.text_content = StatusText();
            }
        }

        /// <summary>输入框右边那句话：没输入时是提示，有输入时是命中条数。</summary>
        string StatusText()
        {
            if (Query.Length == 0)
            {
                return SearchStrings.Text(hintKey);
            }

            if (matchCount == 0)
            {
                return SearchStrings.Text(SearchStrings.NoResult);
            }

            return string.Format(SearchStrings.Text(SearchStrings.Result), matchCount);
        }

        /// <summary>拨一个块的显隐（搜索过滤收起一行的统一做法）；按钮还需额外 hide()/bind()，否则方向键导航仍会走进看不见的行。</summary>
        internal static void SetVisible(DesignerRowMem.DsnMem mem, bool visible)
        {
            if (mem == null || mem.active == visible)
            {
                return;
            }

            aBtn button = mem.Blk as aBtn;
            if (button == null)
            {
                mem.active = visible;
                return;
            }

            // 收起时先 hide、放回时反过来：让 hide/bind 操作 Skin 与焦点时对象还活着。
            if (!visible)
            {
                button.hide();
            }

            mem.active = visible;

            if (visible)
            {
                button.bind();
            }
        }
    }
}
