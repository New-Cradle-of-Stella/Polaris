using System;
using System.IO;
using System.Reflection;
using BepInEx;

namespace Polaris.Infra
{
    /// <summary>
    /// Polaris 约定的目录结构，从 <see cref="PolarisAPI.Paths"/> 取。
    /// <para>
    /// 目录约定是全库共享的事实，也是模组作者需要知道的信息，所以收在这一处并公开——
    /// 之前它散落在几个 internal 的辅助类里，下游模组拿不到，只能各自把路径再拼一遍。
    /// </para>
    /// </summary>
    public sealed class PathsAPI
    {
        internal PathsAPI() { }

        /// <summary>BepInEx 的 plugins 目录。</summary>
        public string PluginsRoot => Paths.PluginPath;

        /// <summary>Polaris 系列自己的根目录：<c>plugins/Polaris/</c>。</summary>
        public string PolarisRoot => Path.Combine(PluginsRoot, "Polaris");

        /// <summary>
        /// Polaris 随包分发的第三方依赖目录：<c>plugins/Polaris/libs/</c>。
        /// <para>
        /// 单独一层，不和 <c>Polaris.dll</c> 挤在 <see cref="PolarisRoot"/> 根下，是为了让
        /// "哪个是插件、哪些只是它的依赖"一眼可辨——目前住在这里的是 NVorbis（.ogg 解码）
        /// 以及它拉进来的 System.Memory / System.Buffers / System.Numerics.Vectors /
        /// System.Runtime.CompilerServices.Unsafe。错误归因也靠这个位置把它们判成
        /// <see cref="Diagnostics.OwnerKind.ModLibrary"/> 而不是可定责的插件。
        /// </para>
        /// <para>
        /// 放在子目录里不影响加载：BepInEx 的运行时解析（Mono 版 <c>LocalResolve</c> →
        /// <c>Utility.TryResolveDllAssembly</c>）对 plugins 目录是连同全部子目录递归查找的，
        /// 只要还在 <see cref="PluginsRoot"/> 底下，放哪一层都找得到。
        /// </para>
        /// </summary>
        public string LibsDir => Path.Combine(PolarisRoot, "libs");

        /// <summary>
        /// 设置项配置文件目录：<c>BepInEx/config/Polaris/</c>。
        /// 放在 config 下而不是 plugins 下，是因为它是用户数据——玩家单独备份 config、
        /// 整包替换 plugins 都不会互相波及。
        /// </summary>
        public string ConfigDir => Path.Combine(Paths.ConfigPath, "Polaris");

        /// <summary>
        /// Polaris 的运行期产出与状态目录：<c>BepInEx/Polaris/</c>。
        /// <para>
        /// 三个位置都考虑过，选在这里的理由：不放 <see cref="PluginsRoot"/> 下——那里的东西
        /// 是"要被加载的插件"，这些是产出物，玩家整包替换 plugins 时不该把自己的报告冲掉；
        /// 也不放 <see cref="ConfigDir"/> 下——它们不是配置，玩家备份 config 时不该顺手带上一堆
        /// 崩溃记录。放在 BepInEx 根下自成一格，玩家要交报告时也好找。
        /// </para>
        /// </summary>
        public string StateDir => Path.Combine(Paths.BepInExRootPath, "Polaris");

        /// <summary>
        /// 错误报告目录：<c>BepInEx/Polaris/reports/</c>。见 <see cref="Diagnostics.ErrorReportWriter"/>。
        /// 单独一层子目录，是为了不和 <see cref="StateDir"/> 里那些运行期状态文件混在一起——
        /// 报告是要交给别人看的，状态文件是 Polaris 自己用的。
        /// </summary>
        public string ReportsDir => Path.Combine(StateDir, "reports");

        /// <summary>
        /// 某个模组的默认资源根：dll 所在目录下、与 dll 同名（不含扩展名）的子文件夹。
        /// 例如 <c>plugins/WNMN/WeNeedMoreNoels.dll</c> → <c>plugins/WNMN/WeNeedMoreNoels/</c>。
        /// <para>
        /// 这个约定不要求资源文件夹一定直接在 <c>plugins/</c> 根下——不管 dll 自己嵌套在几层
        /// 子目录里，资源文件夹永远是"它自己旁边、和它同名"的那一个，肉眼看目录结构就能找到。
        /// </para>
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

            // ReportsDir 在 StateDir 之下，CreateDirectory 会把中间层一起建出来。
            Directory.CreateDirectory(ReportsDir);
        }
    }
}
