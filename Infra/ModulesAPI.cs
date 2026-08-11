using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono.Bootstrap;

namespace Polaris.Infra
{
    /// <summary>
    /// 对 BepInEx 已加载插件的只读视图，从 <see cref="PolarisAPI.Modules"/> 取。
    /// <para>
    /// 存在的理由是把 <c>UnityChainloader.Instance</c> 这个"随时可能还没就绪"的静态入口收在
    /// 一处：调用方不必各自判空，也不必知道 Mono 版 Chainloader 长什么样。全系列的类型扫描
    /// 作用域（见 <see cref="TypesAPI.InPlugins"/>）与错误归因的插件映射
    /// （见 <c>Diagnostics.AssemblyOwnerIndex</c>）都从这里取名单。
    /// </para>
    /// </summary>
    public sealed class ModulesAPI
    {
        internal ModulesAPI() { }

        /// <summary>
        /// 某个 BepInEx 插件是否已加载，用于软依赖判断：
        /// <c>if (PolarisAPI.Modules.IsLoaded("SomeMod")) { ... }</c>。
        /// 传的是插件 GUID（<c>BepInPlugin</c> 的第一个参数），不是程序集名。
        /// </summary>
        public bool IsLoaded(string pluginGuid)
            => !string.IsNullOrEmpty(pluginGuid)
               && UnityChainloader.Instance?.Plugins.ContainsKey(pluginGuid) == true;

        /// <summary>BepInEx 已加载的全部插件。Chainloader 还没就绪时为空集合。</summary>
        public IEnumerable<PluginInfo> Plugins
            => UnityChainloader.Instance?.Plugins.Values ?? Enumerable.Empty<PluginInfo>();

        /// <summary>
        /// 已加载插件所在的程序集（去重）。同一个程序集可能对应多个插件实例，这里只给一次。
        /// 各类扫描器的默认作用域，见 <see cref="TypesAPI.InPlugins"/>。
        /// </summary>
        public IEnumerable<Assembly> PluginAssemblies
            => Plugins.Select(p => p.Instance?.GetType().Assembly)
                      .Where(a => a != null)
                      .Distinct();
    }
}
