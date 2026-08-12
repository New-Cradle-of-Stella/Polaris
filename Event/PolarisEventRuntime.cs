using System;
using System.Collections.Generic;
using System.IO;

namespace Polaris.Event
{
    /// <summary>
    /// Unpacks registered event definitions to real <c>.cmd</c> files under <c>plugins/Polaris/events/</c>;
    /// a Harmony patch (<c>Patch_EV_getEventContent</c>) makes the engine find them there. Pure file I/O, so it
    /// can run right after scanning instead of waiting for <see cref="PolarisEvent.Start"/>/<c>Change</c>.
    /// </summary>
    internal static class PolarisEventRuntime
    {
        static readonly Dictionary<string, string> installed = new Dictionary<string, string>(PolarisEventId.KeyComparer);

        internal static void EnsureAllInstalled()
        {
            foreach (var definition in PolarisEventRegistry.All)
            {
                EnsureInstalled(definition);
            }
        }

        internal static void EnsureInstalled(PolarisEventDefinition definition)
        {
            if (definition == null || installed.ContainsKey(definition.RuntimeKey))
            {
                return;
            }

            try
            {
                string path = ResolveFilePath(definition);
                WriteIfChanged(path, definition.CommandText);
                installed[definition.RuntimeKey] = path;
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "PolarisEvent.EnsureInstalled");
            }
        }

        /// <summary>Used by <c>Patch_EV_getEventContent</c> to find the unpacked file for a runtime key, if any.</summary>
        internal static bool TryGetFilePath(string runtimeKey, out string path)
            => installed.TryGetValue(runtimeKey, out path);

        static string ResolveFilePath(PolarisEventDefinition definition)
        {
            string relative = definition.LogicalId.Replace('/', Path.DirectorySeparatorChar) + ".cmd";
            return Path.Combine(PolarisAPI.Paths.EventsDir, definition.Namespace, relative);
        }

        static void WriteIfChanged(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            if (File.Exists(path) && File.ReadAllText(path) == content)
            {
                return; // Skip rewriting if content is unchanged, to avoid touching mtimes on every startup.
            }

            File.WriteAllText(path, content);
        }
    }
}
