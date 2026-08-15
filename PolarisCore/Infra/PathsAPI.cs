using System;
using System.IO;
using System.Reflection;
using BepInEx;

namespace Polaris.Infra
{
    /// <summary>Polaris 约定的目录结构，从 <see cref="PolarisAPI.Paths"/> 取；公开给模组作者，避免各自重新拼路径。</summary>
    public sealed class PathsAPI
    {
        internal PathsAPI() { }

        /// <summary>BepInEx 的 plugins 目录。</summary>
        public string PluginsRoot => Paths.PluginPath;

        /// <summary>Polaris 系列自己的根目录：<c>plugins/Polaris/</c>。</summary>
        public string PolarisRoot => Path.Combine(PluginsRoot, "Polaris");

        /// <summary>
        /// Polaris 随包分发的第三方依赖目录：<c>plugins/Polaris/libs/</c>。单独一层是为了让"插件 vs 依赖"一眼可辨，
        /// 并让错误归因把这里的程序集判成 <see cref="Diagnostics.OwnerKind.ModLibrary"/> 而非可定责的插件。
        /// </summary>
        public string LibsDir => Path.Combine(PolarisRoot, "libs");

        /// <summary>设置项配置文件目录：<c>BepInEx/config/Polaris/</c>；放在 config 下因为它是用户数据，与 plugins 互不波及。</summary>
        public string ConfigDir => Path.Combine(Paths.ConfigPath, "Polaris");

        /// <summary>Polaris 的运行期产出与状态目录：<c>BepInEx/Polaris/</c>；独立于 plugins（会被整包替换）和 config（会被单独备份）之外。</summary>
        public string StateDir => Path.Combine(Paths.BepInExRootPath, "Polaris");

        /// <summary>错误报告目录：<c>BepInEx/Polaris/reports/</c>（见 <see cref="Diagnostics.ErrorReportWriter"/>），与 <see cref="StateDir"/> 里的内部状态文件分开。</summary>
        public string ReportsDir => Path.Combine(StateDir, "reports");

        /// <summary>
        /// 某个模组的默认资源根：dll 所在目录下、与 dll 同名（不含扩展名）的子文件夹，例如
        /// <c>plugins/WNMN/WeNeedMoreNoels.dll</c> → <c>plugins/WNMN/WeNeedMoreNoels/</c>。
        /// </summary>
        public string DefaultResRootOf(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            string location = assembly.Location;
            string directory = Path.GetDirectoryName(location);
            string name = Path.GetFileNameWithoutExtension(location);
            return Path.Combine(string.IsNullOrEmpty(directory) ? "." : directory, name);
        }

        /// <summary>幂等创建各目录；由 <see cref="Plugin"/> 在 Awake 阶段尽早调用一次。</summary>
        internal void EnsureDirectories()
        {
            Directory.CreateDirectory(LibsDir);
            Directory.CreateDirectory(ConfigDir);

            // ReportsDir 在 StateDir 之下，CreateDirectory 会一并建出中间层。
            Directory.CreateDirectory(ReportsDir);
        }
    }
}
