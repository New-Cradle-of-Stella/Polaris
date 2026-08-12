using System;

namespace Polaris.Event
{
    /// <summary>
    /// 扫描-隔离-冲突模式照抄 <c>Lang\PlangRegistryScanner.cs</c>：一次性开关防重复扫描、只看
    /// <c>PolarisAPI.Types.InPluginsWith</c>（不是全 AppDomain）、每个 registrar 单独 try/catch 隔离、
    /// 扫描结束统一 <see cref="PolarisEventRegistry.Seal"/>。
    /// </summary>
    internal static class PolarisEventRegistryScanner
    {
        static bool scanned;

        internal static void ScanAll()
        {
            if (scanned)
            {
                return;
            }

            scanned = true;

            int count = 0;
            foreach ((Type type, PolarisEventAutoRegistrationAttribute attribute) in
                     PolarisAPI.Types.InPluginsWith<PolarisEventAutoRegistrationAttribute>())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(IPolarisEventRegistrar).IsAssignableFrom(type))
                {
                    continue;
                }

                try
                {
                    var context = new PolarisEventRegistrationContext(attribute.Namespace, type.Assembly);
                    ((IPolarisEventRegistrar)Activator.CreateInstance(type)).Register(context);
                    count++;
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[PolarisEvent] Failed to auto-register {type.FullName}; skipped: {e}");
                }
            }

            Plugin.Logger.LogMessage($"[PolarisEvent] Registered {count} generated event classes.");
            PolarisEventRegistry.Seal();
            PolarisEventRuntime.EnsureAllInstalled();
        }
    }
}
