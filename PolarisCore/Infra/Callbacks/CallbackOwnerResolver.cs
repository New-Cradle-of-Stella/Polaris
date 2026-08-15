using System.Reflection;
using BepInEx;

namespace Polaris.Infra
{
    /// <summary>把一个委托的声明程序集映射回 BepInEx 插件 GUID，用于回调诊断里的责任人字段。</summary>
    internal static class CallbackOwnerResolver
    {
        internal static string ResolveGuid(MethodInfo method)
        {
            Assembly asm = method?.DeclaringType?.Assembly;
            if (asm == null)
            {
                return "unknown";
            }

            foreach (PluginInfo plugin in PolarisAPI.Modules.Plugins)
            {
                if (plugin.Instance != null && plugin.Instance.GetType().Assembly == asm)
                {
                    return plugin.Metadata.GUID;
                }
            }

            // 映射不到任何已加载插件（例如调用方是宿主程序集自己），保留程序集名，不阻止订阅。
            return asm.GetName().Name;
        }
    }
}
