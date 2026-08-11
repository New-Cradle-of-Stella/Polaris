using System;
using System.Collections.Generic;
using System.Linq;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置项 API，从 <see cref="PolarisAPI.Settings"/> 取。
    /// <para>
    /// <b>声明设置项只有一条途径：给静态字段标 <see cref="PolarisSettingAttribute"/></b>
    /// （类上再标一个 <see cref="PolarisSettingGroupAttribute"/>）。不提供运行时注册接口是刻意的——
    /// 原版的 <c>UiCFG</c> 实例只 new 一次、之后一直靠 <c>resume()</c> 复用，
    /// 注册晚于设置界面构造的设置项这一局根本不会出现；把注册时机收归 Polaris 统一掌控
    /// （<see cref="SettingsAttributeScanner.ScanAll"/> 在 <c>Plugin.Start</c> 里固定调用一次），
    /// 模组就不可能踩到这个坑。
    /// </para>
    /// <para>
    /// 声明完的设置项会渲染到原版设置界面（标题画面与 ESC 菜单共用）主标签页的尾部，
    /// 并自动持久化到 <c>BepInEx/config/Polaris/&lt;modId&gt;.cfg</c>。
    /// 这个类本身只负责读写值与查询。
    /// </para>
    /// </summary>
    public class SettingsAPI
    {
        internal SettingsAPI() { }

        readonly List<SettingGroup> groups = [];
        readonly Dictionary<string, SettingGroup> byModId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>原版设置界面是否已经建好。建好之后再注册只能等下一局，这里用来给出明确警告。</summary>
        internal bool ScreenBuilt { get; set; }

        /// <summary>
        /// 开始为某个模组构造设置项（内部用，由 <see cref="SettingsAttributeScanner"/> 调用）。
        /// 同一个 <paramref name="modId"/> 重复调用会继续往已有的组里追加，不会新建一组——
        /// 这样一个模组把设置项分散在几个类里声明，界面上也还是一个分区。
        /// </summary>
        /// <param name="modId">模组标识，直接用作配置文件名，不能含非法文件名字符</param>
        /// <param name="displayName">分区标题，缺省用 <paramref name="modId"/></param>
        /// <param name="order">分区排序权重，小的在前</param>
        /// <remarks>
        /// 名字刻意不叫 <c>For</c>：那个名字留给公开的 <see cref="For(string)"/>（取读写作用域），
        /// 两者都能单参调用，同名会造成重载解析歧义。
        /// </remarks>
        internal SettingsGroupBuilder BuildFor(string modId, string displayName = null, int order = 0)
        {
            if (byModId.TryGetValue(modId, out SettingGroup existing))
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    existing.DisplayName = displayName;
                }

                // order 刻意不覆盖：后来的类多半没显式写权重，用它的默认 0 去盖掉先声明者
                // 精心设的排序，只会让分区位置随扫描顺序漂移。
                return new SettingsGroupBuilder(existing);
            }

            return new SettingsGroupBuilder(new SettingGroup(modId, displayName) { Order = order });
        }

        /// <summary>由 <see cref="SettingsGroupBuilder.Register"/> 调用：登记 + 立刻绑定存储并回灌上次存的值。</summary>
        internal SettingGroup Register(SettingGroup group)
        {
            if (!byModId.ContainsKey(group.ModId))
            {
                byModId[group.ModId] = group;
                groups.Add(group);
            }

            SettingsStore.Bind(group);

            if (ScreenBuilt)
            {
                Plugin.Logger.LogWarning(
                    $"[Polaris.Settings] 组 {group.ModId} 在设置界面建好之后才注册，本局不会显示。" +
                    "请把注册挪到插件的 Awake 里。");
            }

            return group;
        }

        /// <summary>按注册顺序 + <see cref="SettingGroup.Order"/> 排好的分区列表。</summary>
        internal IReadOnlyList<SettingGroup> Groups
            => groups.OrderBy(g => g.Order).ToList();

        internal IEnumerable<ValueSettingDefinition> AllValues
            => groups.SelectMany(g => g.Settings).OfType<ValueSettingDefinition>();

        /// <summary>
        /// 取某个模组的设置读写作用域。作用域本身很轻，随取随用，不需要缓存；
        /// 该模组还没声明过设置项时也能取到，只是 <see cref="SettingsScope.Exists"/> 为 false。
        /// </summary>
        public SettingsScope For(string modId)
        {
            if (string.IsNullOrEmpty(modId))
            {
                throw new ArgumentException("modId 不能为空。", nameof(modId));
            }

            return new SettingsScope(this, modId);
        }

        internal SettingGroup FindGroup(string modId)
            => modId != null && byModId.TryGetValue(modId, out SettingGroup g) ? g : null;

        /// <summary>把所有已注册模组的设置立刻写盘。界面提交时会自动调用，一般不需要手动调。</summary>
        public void Save() => SettingsStore.Commit();
    }
}
