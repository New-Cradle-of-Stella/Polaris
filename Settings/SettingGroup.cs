using System;
using System.Collections.Generic;
using System.IO;

namespace Polaris.Settings
{
    /// <summary>
    /// 一个模组的设置项集合。<see cref="ModId"/> 同时是配置文件名，
    /// <see cref="DisplayName"/> 是渲染到原版设置界面里的分区标题。
    /// </summary>
    public sealed class SettingGroup
    {
        readonly List<SettingDefinition> settings = [];
        readonly Dictionary<string, SettingDefinition> byId = new(StringComparer.Ordinal);

        internal SettingGroup(string modId, string displayName)
        {
            if (string.IsNullOrEmpty(modId))
            {
                throw new ArgumentException("ModId cannot be empty", nameof(modId));
            }

            // ModId 直接拼进文件路径，非法字符会让 ConfigFile 在写盘时炸在很远的地方。
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (modId.IndexOf(c) >= 0)
                {
                    throw new ArgumentException($"ModId contains the illegal file name character '{c}': {modId}", nameof(modId));
                }
            }

            ModId = modId;
            DisplayName = string.IsNullOrEmpty(displayName) ? modId : displayName;
        }

        public string ModId { get; }

        /// <summary>分区标题的原始串，可能是 <c>&amp;</c> 开头的本地化键。</summary>
        public string DisplayName { get; internal set; }

        /// <summary>
        /// 按当前语言求值之后的分区标题。求值推迟到这里而不是 <see cref="DisplayName"/> 赋值时，
        /// 理由与 <see cref="SettingDefinition"/> 的 <c>Display*</c> 一致：注册发生在启动阶段，
        /// 那时游戏的语言表未必已经建好。
        /// </summary>
        public string DisplayTitle => PolarisAPI.Localization.Text(DisplayName);

        /// <summary>分区之间的排序权重，小的在前；相同则按注册先后。</summary>
        public int Order { get; internal set; }

        public IReadOnlyList<SettingDefinition> Settings => settings;

        internal void Add(SettingDefinition setting)
        {
            if (byId.ContainsKey(setting.Id))
            {
                throw new ArgumentException($"Group {ModId} already has a setting with Id {setting.Id}");
            }

            setting.Group = this;
            settings.Add(setting);
            byId[setting.Id] = setting;
        }

        public bool TryGet(string id, out SettingDefinition setting) => byId.TryGetValue(id, out setting);

        /// <summary>按 Id 取强类型设置项；类型或 Id 对不上返回 null。</summary>
        public T Entry<T>(string id) where T : SettingDefinition
            => byId.TryGetValue(id, out SettingDefinition s) ? s as T : null;
    }
}
