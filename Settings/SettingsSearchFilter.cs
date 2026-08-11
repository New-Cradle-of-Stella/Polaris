using System;
using System.Collections.Generic;
using nel;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置界面的搜索过滤：记住 <see cref="PolarisSettingsScreen.Append"/> 画出来的每一个块属于
    /// 哪个分区/哪个设置项，然后按查询串把不匹配的行整行收起来。
    /// <para>
    /// 收起靠的是原版行管理器自己的机制，不是"把控件挪到屏幕外"也不是重建界面：
    /// <c>DesignerRowMem.DsnMem.active</c> 置 false 的块在
    /// <c>Remake()</c> 重排时<b>不占位</b>（<c>DesignerRowMem.Add</c> 里 <c>PreVal.Push/Pop</c> 那一对
    /// 就是干这个的），于是剩下的行会自动收拢上来，滚动范围也跟着 <c>rowRemakeCheck</c> 重算。
    /// 换句话说，过滤这件事原版本来就支持，这里只是把开关拨到位。
    /// </para>
    /// <para>
    /// <b>为什么不重建界面</b>：主标签页里绝大多数行是原版自己画的（窗口模式、音量、自动存档……），
    /// 重建就得连它们一起重跑 <c>createBoxDesignerContentMain</c>，那是一大堆带副作用的初始化。
    /// 只拨可见性则完全不碰原版那些行。
    /// </para>
    /// <para>
    /// 过滤范围<b>只含 Polaris 注册的设置项</b>：原版那些行始终原样留在上面。原版的标签与控件之间
    /// 没有任何显式关联（全靠 <c>P("Config_Xxx").addSliderCT(...)</c> 的书写顺序），要过滤就得靠
    /// 猜哪个标签配哪个控件，猜错的代价是把一行拆散在界面上。
    /// </para>
    /// <para>
    /// 全局单例：同一时刻只可能有一个设置界面立着（标题画面与 ESC 菜单不会同时开），
    /// 每次 <see cref="Begin"/> 都把上一次的登记整个丢掉。
    /// </para>
    /// </summary>
    internal static class SettingsSearchFilter
    {
        /// <summary>一个设置项在界面上占用的那几个块（标签、控件本体、值显示区……）。</summary>
        internal sealed class RowRecorder
        {
            internal RowRecorder(SettingDefinition setting)
            {
                Setting = setting;
            }

            internal SettingDefinition Setting { get; }

            internal List<DesignerRowMem.DsnMem> Blocks { get; } = [];

            /// <summary>登记一个块。传 null 是允许的（渲染分支没画出这个块），直接忽略。</summary>
            internal void Add(IDesignerBlock block) => Remember(Blocks, block);
        }

        /// <summary>一个分区：分隔线 + 标题这两个"头部块"，加上它下面的所有设置行。</summary>
        internal sealed class GroupRecorder
        {
            internal GroupRecorder(SettingGroup group)
            {
                Group = group;
            }

            internal SettingGroup Group { get; }

            internal List<DesignerRowMem.DsnMem> Header { get; } = [];

            internal List<RowRecorder> Rows { get; } = [];

            internal void AddHeader(IDesignerBlock block) => Remember(Header, block);

            internal RowRecorder OpenRow(SettingDefinition setting)
            {
                var row = new RowRecorder(setting);
                Rows.Add(row);
                return row;
            }
        }

        static readonly List<GroupRecorder> groups = [];

        /// <summary>真正持有这些块的行管理器所在的 designer（主标签页），重排要对它调。</summary>
        static Designer tab;

        /// <summary>
        /// 主标签页的行管理器。显隐开关就挂在它的 <c>DsnMem</c> 上，所以登记时就把
        /// <c>DsnMem</c> 取出来存好，之后不再回头查。
        /// <para>
        /// <b>不能事后靠 <c>Designer.getDesignerBlockMemory</c> 现查</b>：那张表
        /// （<c>OBlockMem</c>）在 <c>BxOut</c> 身上，而游戏内每次切换菜单分类
        /// <c>UiGameMenu.BxRRemake</c> 都会 <c>BxR.Clear()</c> 一次，把它整个清空——
        /// 重新登记回去的只有标签页自己，不含标签页<b>里面</b>的这些块。
        /// 反过来标签页的行管理器是安全的：被清的是 <c>BxR.Row</c>，不是它。
        /// </para>
        /// </summary>
        static DesignerRowMem rows;

        /// <summary>当前查询命中的设置项条数，供搜索栏右侧的状态文字用。</summary>
        internal static int MatchCount { get; private set; }

        /// <summary>
        /// 开始一轮登记。由 <see cref="PolarisSettingsScreen.Append"/> 在往主标签页里画东西之前调用。
        /// <para>
        /// 这里必须取 <c>CurrentAttachTarget</c> 而不是 <paramref name="cfg"/><c>.BxOut</c> 本身：
        /// 调用点在 <c>addTab</c>/<c>endTab</c> 之间，块进的是那个 tab 的行管理器，
        /// 而 <c>BxOut.Row</c> 里躺着的是 tab 自己。重排要对持有块的那一个调才有意义。
        /// </para>
        /// </summary>
        internal static void Begin(UiCFG cfg)
        {
            groups.Clear();
            MatchCount = 0;
            tab = cfg.BxOut?.CurrentAttachTarget;
            rows = tab?.getRowManager();
        }

        /// <summary>
        /// 把一个刚画出来的块记进 <paramref name="into"/>。存的是它在行管理器里的
        /// <c>DsnMem</c>——那才是显隐开关本身，理由见 <see cref="rows"/>。
        /// </summary>
        static void Remember(List<DesignerRowMem.DsnMem> into, IDesignerBlock block)
        {
            if (block == null || rows == null)
            {
                return;
            }

            DesignerRowMem.DsnMem mem = rows.getBlockMemory(block);
            if (mem != null)
            {
                into.Add(mem);
            }
        }

        internal static GroupRecorder OpenGroup(SettingGroup group)
        {
            var recorder = new GroupRecorder(group);
            groups.Add(recorder);
            return recorder;
        }

        /// <summary>
        /// 界面关掉了：丢掉登记表。<b>不</b>在这里恢复可见性——块可能已经被 <c>Clear</c> 掉了，
        /// 而且下次打开会重新 <see cref="Begin"/>。"关掉时把过滤撤销"是搜索栏自己的事
        /// （<see cref="PolarisSearchRow.Reset"/>）。
        /// </summary>
        internal static void Forget()
        {
            groups.Clear();
            tab = null;
            rows = null;
            MatchCount = 0;
        }

        /// <summary>
        /// 按查询串重算每一行的显隐并重排。空串等于"全部显示"。
        /// <para>
        /// 分区标题命中时该分区下<b>所有</b>设置项都显示——按模组名搜就是要看那个模组的全部设置。
        /// 分区的头部（分隔线 + 标题）只在这个分区至少留下一行时才显示，否则会剩下一串空标题。
        /// </para>
        /// </summary>
        internal static void Apply(string query)
        {
            if (tab == null)
            {
                MatchCount = 0;
                return;
            }

            string[] tokens = SettingsSearchQuery.Tokenize(query);
            int matched = 0;

            try
            {
                foreach (GroupRecorder group in groups)
                {
                    bool groupHit = SettingsSearchQuery.Matches(group.Group.DisplayTitle, tokens);
                    bool anyRow = false;

                    foreach (RowRecorder row in group.Rows)
                    {
                        bool hit = groupHit || SettingsSearchQuery.MatchesAny(
                            tokens, row.Setting.DisplayLabel, row.Setting.DisplayDescription);

                        anyRow |= hit;
                        if (hit)
                        {
                            matched++;
                        }

                        foreach (DesignerRowMem.DsnMem block in row.Blocks)
                        {
                            PolarisSearchRow.SetVisible(block, hit);
                        }
                    }

                    foreach (DesignerRowMem.DsnMem block in group.Header)
                    {
                        PolarisSearchRow.SetVisible(block, anyRow);
                    }
                }

                // force：块的尺寸一个都没变，row_remake_flag 不会自己立起来，
                // 而"哪些块参与排版"变了恰恰只有重排一遍才看得出来。
                tab.rowRemakeCheck(force: true);
            }
            catch (Exception e)
            {
                // 过滤画崩了不能把设置界面一起带走——最坏的结果是界面停在半过滤的样子，
                // 玩家清掉搜索框就能恢复。
                PolarisAPI.Errors.Report(e, "filtering the settings screen");
                Plugin.Logger.LogError($"[Polaris.Settings] Failed to apply the search filter \"{query}\".");
            }

            MatchCount = matched;
        }
    }
}
