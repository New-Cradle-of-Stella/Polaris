using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono.Bootstrap;

namespace Polaris.Infra
{
    /// <summary>对 BepInEx 已加载插件的只读视图，从 <see cref="PolarisAPI.Modules"/> 取；集中处理 <c>UnityChainloader.Instance</c> 可能未就绪的判空。</summary>
    public sealed class ModulesAPI
    {
        internal ModulesAPI() { }

        /// <summary>某个 BepInEx 插件是否已加载，用于软依赖判断；传的是插件 GUID，不是程序集名。</summary>
        public bool IsLoaded(string pluginGuid)
            => !string.IsNullOrEmpty(pluginGuid)
               && UnityChainloader.Instance?.Plugins.ContainsKey(pluginGuid) == true;

        /// <summary>BepInEx 已加载的全部插件。Chainloader 还没就绪时为空集合。</summary>
        public IEnumerable<PluginInfo> Plugins
            => UnityChainloader.Instance?.Plugins.Values ?? Enumerable.Empty<PluginInfo>();

        /// <summary>已加载插件所在的程序集（去重）；各类扫描器的默认作用域，见 <see cref="TypesAPI.InPlugins"/>。</summary>
        public IEnumerable<Assembly> PluginAssemblies
            => Plugins.Select(p => p.Instance?.GetType().Assembly)
                      .Where(a => a != null)
                      .Distinct();
    }
}
