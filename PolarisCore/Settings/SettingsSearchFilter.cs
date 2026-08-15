using System;
using System.Collections.Generic;
using nel;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置界面的搜索过滤：记住每个块属于哪个分区/设置项，按查询串把不匹配的行收起来（置 <c>DsnMem.active = false</c>，靠原版行管理器的重排机制不占位，不重建界面）。
    /// 过滤范围只含 Polaris 注册的设置项，原版行始终原样保留。全局单例，同一时刻只有一个设置界面立着。
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
        /// 主标签页的行管理器，登记时取出 <c>DsnMem</c> 存好，不事后现查——那张表在游戏内切换菜单分类时会被清空，标签页自己的行管理器则安全。
        /// </summary>
        static DesignerRowMem rows;

        /// <summary>当前查询命中的设置项条数，供搜索栏右侧的状态文字用。</summary>
        internal static int MatchCount { get; private set; }

        /// <summary>
        /// 开始一轮登记，由 <see cref="PolarisSettingsScreen.Append"/> 在画东西之前调用；取 <c>CurrentAttachTarget</c> 而非 <c>BxOut</c> 本身，因块实际进的是那个 tab 的行管理器。
        /// </summary>
        internal static void Begin(UiCFG cfg)
        {
            groups.Clear();
            MatchCount = 0;
            tab = cfg.BxOut?.CurrentAttachTarget;
            rows = tab?.getRowManager();
        }

        /// <summary>把一个刚画出的块记进 <paramref name="into"/>，存的是它在行管理器里的 <c>DsnMem</c>（显隐开关本身）。</summary>
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

        /// <summary>界面关掉时丢掉登记表；不在此恢复可见性，撤销过滤是搜索栏自己的事（<see cref="PolarisSearchRow.Reset"/>）。</summary>
        internal static void Forget()
        {
            groups.Clear();
            tab = null;
            rows = null;
            MatchCount = 0;
        }

        /// <summary>
        /// 按查询串重算每行显隐并重排（空串=全部显示）；分区标题命中时该分区全显，分区头部仅在留有可见行时显示。
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

                // force：块尺寸未变，row_remake_flag 不会自动立起，须强制重排才能反映可见性变化。
                tab.rowRemakeCheck(force: true);
            }
            catch (Exception e)
            {
                // 过滤画崩不能带垮整个设置界面；最坏情况是停在半过滤状态，清空搜索框可恢复。
                PolarisAPI.Errors.Report(e, "filtering the settings screen");
                Plugin.Logger.LogError($"[Polaris.Settings] Failed to apply the search filter \"{query}\".");
            }

            MatchCount = matched;
        }
    }
}
