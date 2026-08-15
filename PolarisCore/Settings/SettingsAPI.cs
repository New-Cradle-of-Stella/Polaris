using System;
using System.Collections.Generic;
using System.Linq;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置项 API，从 <see cref="PolarisAPI.Settings"/> 取。声明设置项只有一条途径：给静态字段标 <see cref="PolarisSettingAttribute"/>（类上再标 <see cref="PolarisSettingGroupAttribute"/>），
    /// 统一由 <see cref="SettingsAttributeScanner.ScanAll"/> 在 <c>Plugin.Start</c> 扫描注册，避免注册晚于设置界面构造导致本局不生效。
    /// 声明后的设置项自动渲染到原版设置界面并持久化到 <c>BepInEx/config/Polaris/&lt;modId&gt;.cfg</c>。
    /// </summary>
    public class SettingsAPI
    {
        internal SettingsAPI() { }

        readonly List<SettingGroup> groups = [];
        readonly Dictionary<string, SettingGroup> byModId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>原版设置界面是否已经建好。建好之后再注册只能等下一局，这里用来给出明确警告。</summary>
        internal bool ScreenBuilt { get; set; }

        /// <summary>
        /// 开始为某个模组构造设置项（内部用，由 <see cref="SettingsAttributeScanner"/> 调用）；同一 <paramref name="modId"/> 重复调用追加到已有组，不新建组。
        /// </summary>
        /// <param name="modId">模组标识，直接用作配置文件名，不能含非法文件名字符</param>
        /// <param name="displayName">分区标题，缺省用 <paramref name="modId"/></param>
        /// <param name="order">分区排序权重，小的在前</param>
        internal SettingsGroupBuilder BuildFor(string modId, string displayName = null, int order = 0)
        {
            if (byModId.TryGetValue(modId, out SettingGroup existing))
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    existing.DisplayName = displayName;
                }

                // order 不覆盖，避免后来类的默认权重 0 打乱先声明者设定的排序。
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
                    $"[Polaris.Settings] Group {group.ModId} registered after the settings screen was built, so it will not show this session. " +
                    "Move the registration into the plugin's Awake.");
            }

            return group;
        }

        /// <summary>按注册顺序 + <see cref="SettingGroup.Order"/> 排好的分区列表。</summary>
        internal IReadOnlyList<SettingGroup> Groups
            => groups.OrderBy(g => g.Order).ToList();

        internal IEnumerable<ValueSettingDefinition> AllValues
            => groups.SelectMany(g => g.Settings).OfType<ValueSettingDefinition>();

        /// <summary>取某个模组的设置读写作用域；很轻，无需缓存；模组未声明过设置时仍可取到，<see cref="SettingsScope.Exists"/> 为 false。</summary>
        public SettingsScope For(string modId)
        {
            if (string.IsNullOrEmpty(modId))
            {
                throw new ArgumentException("modId cannot be empty.", nameof(modId));
            }

            return new SettingsScope(this, modId);
        }

        internal SettingGroup FindGroup(string modId)
            => modId != null && byModId.TryGetValue(modId, out SettingGroup g) ? g : null;

        /// <summary>把所有已注册模组的设置立刻写盘。界面提交时会自动调用，一般不需要手动调。</summary>
        public void Save() => SettingsStore.Commit();
    }
}
