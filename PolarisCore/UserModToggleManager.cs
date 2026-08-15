using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Polaris
{
    /// <summary>管理 plugins 根目录下 dll 的启停：改名在 .dll 与 .dll.disabled 之间切换，下次启动才会生效。</summary>
    internal static class UserModToggleManager
    {
        const string DisabledSuffix = ".disabled";

        /// <summary>扫描 <c>plugins</c> 根目录，按去掉 <c>.disabled</c> 后缀的文件名归并出启停记录。</summary>
        internal static List<UserModRecord> Scan()
        {
            string selfFileName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            var byDisplayName = new Dictionary<string, UserModRecord>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(PolarisAPI.Paths.PluginsRoot))
            {
                return [];
            }

            foreach (string path in Directory.GetFiles(PolarisAPI.Paths.PluginsRoot, "*.dll*", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(path);
                bool isDisabled = fileName.EndsWith(".dll" + DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                bool isEnabled = !isDisabled && fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                if (!isDisabled && !isEnabled)
                {
                    continue;
                }

                string displayName = isDisabled
                    ? fileName.Substring(0, fileName.Length - DisabledSuffix.Length)
                    : fileName;

                // 不允许玩家把 Polaris 自己禁用掉。
                if (string.Equals(displayName, selfFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!byDisplayName.TryGetValue(displayName, out UserModRecord record))
                {
                    record = new UserModRecord
                    {
                        DisplayName = displayName,
                        EnabledPath = Path.Combine(PolarisAPI.Paths.PluginsRoot, displayName),
                        DisabledPath = Path.Combine(PolarisAPI.Paths.PluginsRoot, displayName + DisabledSuffix),
                        Info = PolarisModInfoResolver.Resolve(displayName),
                    };
                    byDisplayName[displayName] = record;
                }

                record.Enabled = isEnabled;
            }

            return byDisplayName.Values.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>把文件改名到目标启停状态；已在目标状态直接返回成功，失败记到 <see cref="UserModRecord.Error"/> 并记日志，不抛异常。</summary>
        internal static bool SetEnabled(UserModRecord record, bool enabled)
        {
            if (record.Enabled == enabled)
            {
                record.Error = null;
                return true;
            }

            string from = enabled ? record.DisabledPath : record.EnabledPath;
            string to = enabled ? record.EnabledPath : record.DisabledPath;

            try
            {
                File.Move(from, to);
                record.Enabled = enabled;
                record.Error = null;
                return true;
            }
            catch (Exception ex)
            {
                record.Error = ex.Message;
                Plugin.Logger.LogWarning($"[Polaris] Failed to toggle mod \"{record.DisplayName}\": {ex.Message}");
                return false;
            }
        }
    }
}
