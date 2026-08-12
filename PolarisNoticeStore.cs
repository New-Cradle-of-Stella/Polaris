using System;
using System.IO;
using BepInEx.Configuration;

namespace Polaris
{
    /// <summary>
    /// <c>_polaris_notice.cfg</c> 的唯一 <see cref="ConfigFile"/> 实例，供 <see cref="PolarisModWarning"/> 与
    /// <see cref="PolarisErrorNotice"/> 共用；必须共享同一实例，否则后保存的会把先保存的值覆盖回旧值。
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
