using System;

namespace Polaris.Event
{
    /// <summary>
    /// Scans plugin types once for auto-registration attributes, isolating each registrar with its own try/catch,
    /// then seals the registry via <see cref="PolarisEventRegistry.Seal"/>.
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
