using System;
using System.IO;
using BepInEx.Configuration;

namespace Polaris
{
    /// <summary>
    /// <c>_polaris_notice.cfg</c> 的唯一 <see cref="ConfigFile"/> 实例，供标题画面的一次性
    /// 告知页共用（目前是 <see cref="PolarisModWarning"/> 和 <see cref="PolarisErrorNotice"/>）。
    /// <para>
    /// 两个类必须共享同一个实例，不能各自 <c>new ConfigFile(同一路径)</c>：BepInEx 的
    /// <see cref="ConfigFile"/> 只把"自己 Bind 过的键"当成活的，其它键只是构造那一刻读到的
    /// 只读快照。如果两个实例各绑各的键，后保存的那个会把先保存的那个刚写的值覆盖回
    /// 构造时的旧值——同样的坑 <see cref="Settings.SettingsStore"/> 按 <c>modId + ".cfg"</c>
    /// 一模一样地存在过，这里用共享单例而不是再撞一次下划线文件名。
    /// </para>
    /// </summary>
    internal static class PolarisNoticeStore
    {
        const string FileName = "_polaris_notice.cfg";

        static ConfigFile file;
        static bool resolved;

        /// <summary>打不开时为 null；调用方按"存不了就不存，不影响本局"处理。</summary>
        internal static ConfigFile File
        {
            get
            {
                if (resolved)
                {
                    return file;
                }

                resolved = true;

                try
                {
                    Directory.CreateDirectory(PolarisAPI.Paths.ConfigDir);
                    file = new ConfigFile(Path.Combine(PolarisAPI.Paths.ConfigDir, FileName), saveOnInit: true);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[Polaris] Failed to open {FileName}; the state of the title-screen notice pages cannot be saved: {e}");
                    file = null;
                }

                return file;
            }
        }
    }
}
