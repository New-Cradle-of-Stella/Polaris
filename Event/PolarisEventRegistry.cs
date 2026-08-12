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
                // 保留先注册的一份而不是让后来者覆盖，做法与 PlangConflictGuard 一致：
                // 至少让"哪一份生效"在同一次启动内是稳定的。
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

        /// <summary>
        /// 供字符串形式的 <c>PolarisEvent.Start("MuseumEntrance")</c> 推断调用方命名空间：
        /// 一个模组项目只有一个 <c>PolarisEventNamespace</c>，因此"调用方程序集 -> 命名空间"是稳定的多对一映射。
        /// </summary>
        internal static string NamespaceOf(Assembly assembly)
            => assembly != null && namespaceByAssembly.TryGetValue(assembly, out var ns) ? ns : null;

        internal static void Seal() => PolarisEventConflictGuard.Seal();
    }
}
