using System;
using Polaris.Localization;
using XX;

namespace Polaris
{
    /// <summary>
    /// 一行搜索栏：标签 + 输入框 + 右侧状态文字。设置界面底部那条
    /// （<see cref="Settings.SettingsSearchBox"/>）和模组管理页列表上方那条
    /// （<see cref="PolarisManagementUI"/>）是同一份实现的两个实例。
    /// <para>
    /// 输入走 <c>fnChangedDelay</c> 而不是 <c>fnChanged</c>，有两个原因：一是
    /// <c>fnChanged</c> 触发时 <c>LabeledInputField.text</c> 还没写成新值（原版是先跑回调再赋值），
    /// 拿到的是上一次的内容；二是它天然带防抖——把 <c>changed_delay_maxt</c> 调小到几帧，
    /// 连打时只在停手后过滤一次，而不是每敲一个字就重排一遍整页。
    /// </para>
    /// <para>
    /// <b>过滤回调里不要重建所在的 designer</b>：那会把正在输入的这个控件本身销毁掉，
    /// 玩家打到一半焦点就没了。两个调用点都是就地拨块的显隐（见 <see cref="SetVisible"/>）
    /// 再 <c>rowRemakeCheck</c>，控件原封不动。
    /// </para>
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
        /// <param name="onQuery">
        /// 查询变化时调用，参数是新的查询串，<b>返回命中条数</b>（用来写右侧的状态文字）。
        /// 真正的过滤由它负责，本类不碰被搜的内容。
        /// </param>
        internal PolarisSearchRow(string name, string hintKey, Func<string, int> onQuery)
        {
            this.name = name;
            this.hintKey = hintKey;
            this.onQuery = onQuery;
        }

        /// <summary>当前查询串（原始输入，未切词）。空串表示没有过滤。</summary>
        internal string Query { get; private set; } = "";

        /// <summary>
        /// 把搜索栏画进 <paramref name="box"/>。调用方负责保证这个 designer 已经 <c>init()</c> 过。
        /// 重建界面时重画一遍即可，<see cref="Query"/> 会被带进新的输入框。
        /// </summary>
        internal void Build(Designer box)
        {
            SearchStrings.Register();

            // 上一次画的那两个控件已经跟着旧 designer 一起没了，先松手再画，
            // 免得中途出异常时留下指向已销毁对象的引用。
            field = null;
            status = null;

            box.alignx = ALIGN.LEFT;

            // 必须在放下第一个块之前取：Designer.use_w 一旦当前行里有了内容，返回的就是
            // "这一行还剩多宽"而不是"框内有多宽"，放到下面去算会把标签的宽度扣两遍。
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

        /// <summary>
        /// 清空搜索并把所有行放回来。界面收起时调用——查询留到下次打开会让玩家对着一份
        /// "缺了大半"的列表发懵，而搜索框那点内容不值得跨次保留。
        /// </summary>
        internal void Reset()
        {
            if (Query.Length == 0)
            {
                return;
            }

            // Unity 的假 null：控件可能已经随 designer 一起销毁了，必须用 != null 走重载。
            if (field != null)
            {
                // call_changed_delay: false——这里是"程序改的"，不该再绕一圈回调，
                // 下面 Apply 已经直接把过滤撤销了。
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

        /// <summary>
        /// 拨一个块的显隐——搜索过滤"收起一行"的统一做法。
        /// <para>
        /// <c>DsnMem.active</c> 置 false 的块在 <c>Remake()</c> 重排时不占位
        /// （<c>DesignerRowMem.Add</c> 里 <c>PreVal.Push/Pop</c> 那一对就是干这个的），
        /// 于是剩下的行会自动收拢上来。但那只管画不画：按钮还得额外 <c>hide()</c>/<c>bind()</c>
        /// ——原版的方向键导航是按 <c>aBtn.isActive()</c> 跳过节点的
        /// （见 <c>aBtn.simulateNaviTranslation</c>），而那个标志只有 <c>hide()</c>/<c>bind()</c>
        /// 会动，光把 GameObject 关掉的话焦点还是会走进看不见的行里。
        /// </para>
        /// </summary>
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

            // 收起时先 hide 再关 GameObject、放回时反过来：hide/bind 会去动 Skin 与焦点，
            // 让它们跑在对象还活着的时候。
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
