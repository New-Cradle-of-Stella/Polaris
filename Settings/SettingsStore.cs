using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置项的持久化。后端直接用 BepInEx 的 <see cref="ConfigFile"/>：它随
    /// <c>BepInEx.Unity.Mono</c> 一起进来，不引入新依赖，自带类型转换、默认值、注释和读写，
    /// 产出的 <c>.cfg</c> 玩家还能手改。每个模组一个文件，路径见 <see cref="PolarisAPI.Paths.ConfigDir"/>。
    /// <para>
    /// 生命周期严格对齐原版 <c>UiCFG</c>：改动即时生效（写内存 + 回写模组字段），
    /// <c>submitData</c> 时才 <see cref="Commit"/> 落盘，<c>revertData</c> 时 <see cref="Revert"/>
    /// 回滚到打开界面那一刻的快照。所以把 <see cref="ConfigFile.SaveOnConfigSet"/> 关掉是必须的，
    /// 否则玩家每拖一格滑块都会写一次盘，而且"取消"也来不及撤销已经写进去的值。
    /// </para>
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
                // 已绑过的跳过：同一个 modId 允许分多次注册，重复 Bind 会白白抛异常。
                if (setting is not ValueSettingDefinition value || value.Entry != null)
                {
                    continue;
                }

                try
                {
                    value.Entry = value.BindTo(file, Section);
                    // 回灌。notify: false —— 此刻模组的 Changed 订阅多半还没挂上，
                    // 而且"加载"不是"玩家改了值"，不该触发变更回调。
                    value.Apply(value.Entry.BoxedValue, notify: false);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[Polaris.Settings] Failed to bind {group.ModId}.{setting.Id}; this entry will not be saved: {e}");
                }
            }

            // 绑完立刻写一次盘。ConfigFile 构造时文件里还一个条目都没有（写出来是空的），
            // 而 SaveOnConfigSet 又被关掉了，不在这里补一次的话，玩家没进过设置界面就一直是个空文件——
            // 既不能手改，模组用 Settings.Set 改的值也落不了盘。
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
                // 真的回滚了才通知——模组需要知道值被改回去了，好把运行中的效果一起撤销；
                // 但玩家一进设置界面就点"取消"是常事，不能借此把每个模组的每个 OnChanged 都叫一遍。
                // 字段无论如何都要回写：模组可能绕过 API 直接改了自己的静态字段。
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
