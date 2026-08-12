using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Event
{
    internal static class PolarisEventRegistry
    {
        static readonly Dictionary<string, PolarisEventDefinition> byRuntimeKey =
            new Dictionary<string, PolarisEventDefinition>(PolarisEventId.KeyComparer);

        static readonly Dictionary<Assembly, string> namespaceByAssembly = new Dictionary<Assembly, string>();

        internal static void Register(PolarisEventDefinition definition)
        {
            if (byRuntimeKey.TryGetValue(definition.RuntimeKey, out var existing) && existing.OwnerAssembly != definition.OwnerAssembly)
            {
                // Keep the first registration instead of letting a later one overwrite it, so the winner stays stable within a session.
                PolarisEventConflictGuard.Record(definition.RuntimeKey, existing.OwnerAssembly, definition.OwnerAssembly);
                return;
            }

            byRuntimeKey[definition.RuntimeKey] = definition;
            if (definition.OwnerAssembly != null)
            {
                namespaceByAssembly[definition.OwnerAssembly] = definition.Namespace;
            }
        }

        internal static bool TryGet(string runtimeKey, out PolarisEventDefinition definition)
            => byRuntimeKey.TryGetValue(runtimeKey, out definition);

        internal static IEnumerable<PolarisEventDefinition> All => byRuntimeKey.Values;

        /// <summary>Infers a caller's namespace for string-based calls like <c>PolarisEvent.Start("MuseumEntrance")</c>.</summary>
        internal static string NamespaceOf(Assembly assembly)
            => assembly != null && namespaceByAssembly.TryGetValue(assembly, out var ns) ? ns : null;

        internal static void Seal() => PolarisEventConflictGuard.Seal();
    }
}
