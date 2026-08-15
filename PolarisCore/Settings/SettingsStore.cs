using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置项的持久化，后端用 BepInEx 的 <see cref="ConfigFile"/>，每个模组一个文件。
    /// 生命周期对齐原版 <c>UiCFG</c>：改动即时写内存/字段，<c>submitData</c> 时 <see cref="Commit"/> 落盘，<c>revertData</c> 时 <see cref="Revert"/> 回滚到快照；因此关闭 <see cref="ConfigFile.SaveOnConfigSet"/>，避免每次拖动都写盘且无法撤销。
    /// </summary>
    internal static class SettingsStore
    {
        /// <summary>配置文件里的节名。一个模组一个文件，不需要再按节细分。</summary>
        const string Section = "Settings";

        static readonly Dictionary<string, ConfigFile> files = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>打开设置界面那一刻的值，用于"取消"回滚。</summary>
        static readonly Dictionary<ValueSettingDefinition, object> snapshot = [];

        /// <summary>把一组设置项绑到它的配置文件上，并把存档里的值回灌回去（特性轨会写回模组的静态字段）。</summary>
        internal static void Bind(SettingGroup group)
        {
            ConfigFile file;
            try
            {
                file = GetFile(group.ModId);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris.Settings] Failed to open the config file of {group.ModId}; this group will not be saved: {e}");
                return;
            }

            foreach (SettingDefinition setting in group.Settings)
            {
                // 已绑过的跳过：同一 modId 允许分多次注册。
                if (setting is not ValueSettingDefinition value || value.Entry != null)
                {
                    continue;
                }

                try
                {
                    value.Entry = value.BindTo(file, Section);
                    // notify: false——加载不是玩家改值，不该触发变更回调。
                    value.Apply(value.Entry.BoxedValue, notify: false);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[Polaris.Settings] Failed to bind {group.ModId}.{setting.Id}; this entry will not be saved: {e}");
                }
            }

            // 绑完立刻写一次盘：SaveOnConfigSet 已关闭，否则玩家没进过设置界面前文件一直是空的。
            try
            {
                file.Save();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris.Settings] Failed to write out the default config of {group.ModId}: {e}");
            }
        }

        /// <summary>拍一张快照作为回滚基准。在设置界面构造与 resume 时各调一次。</summary>
        internal static void Snapshot()
        {
            snapshot.Clear();
            foreach (ValueSettingDefinition v in PolarisAPI.Settings.AllValues)
            {
                snapshot[v] = v.BoxedValue;
            }
        }

        /// <summary>落盘。对应原版 <c>UiCFG.submitData</c> 里的 <c>CFG.saveSdFile()</c>。</summary>
        internal static void Commit()
        {
            foreach (KeyValuePair<string, ConfigFile> kv in files)
            {
                try
                {
                    kv.Value.Save();
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[Polaris.Settings] Failed to save the config of {kv.Key}: {e}");
                }
            }

            // 提交之后，已落盘的值成为新的回滚基准。
            Snapshot();
        }

        /// <summary>回滚到快照。对应原版 <c>UiCFG.revertData</c>；不落盘，磁盘上还是上次提交的内容。</summary>
        internal static void Revert()
        {
            foreach (KeyValuePair<ValueSettingDefinition, object> kv in snapshot)
            {
                // 只在值真正变化时通知，避免"进来就取消"触发所有 OnChanged；字段无论如何都要回写。
                kv.Key.Apply(kv.Value, notify: !Equals(kv.Key.BoxedValue, kv.Value));
            }
        }

        static ConfigFile GetFile(string modId)
        {
            if (files.TryGetValue(modId, out ConfigFile existing))
            {
                return existing;
            }

            Directory.CreateDirectory(PolarisAPI.Paths.ConfigDir);
            var file = new ConfigFile(Path.Combine(PolarisAPI.Paths.ConfigDir, modId + ".cfg"), saveOnInit: true)
            {
                // 见类注释：写盘时机由 Commit 决定，不能每次赋值都写。
                SaveOnConfigSet = false,
            };
            files[modId] = file;
            return file;
        }
    }
}
