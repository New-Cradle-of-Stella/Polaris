using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Polaris.Components
{
    /// <summary>装载并驱动与 PolarisCore 一同分发的普通 DLL 组件。</summary>
    internal static class ComponentHost
    {
        static readonly List<Assembly> assemblies = [];
        static readonly List<PolarisComponent> components = [];
        static readonly List<PolarisComponent> bootstrapComponents = [];
        static readonly List<PolarisComponent> awakeComponents = [];
        static readonly List<PolarisComponent> startComponents = [];
        static readonly List<PolarisComponent> updateComponents = [];
        static readonly List<PolarisComponent> lateUpdateComponents = [];
        static readonly List<PolarisComponent> shutdownComponents = [];

        internal static IReadOnlyList<Assembly> Assemblies => assemblies;

        internal static void Discover()
        {
            if (assemblies.Count != 0)
            {
                return;
            }

            Assembly core = typeof(ComponentHost).Assembly;
            assemblies.Add(core);

            string root = Path.GetDirectoryName(core.Location);
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            var files = new List<string>();
            Collect(root, files);
            Collect(Path.Combine(root, "libs"), files);

            foreach (string file in files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(file);
                    if (!name.Name.StartsWith("Polaris", StringComparison.Ordinal)
                        || string.Equals(name.Name, core.GetName().Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => string.Equals(a.GetName().Name, name.Name, StringComparison.Ordinal))
                        ?? Assembly.LoadFrom(file);

                    if (!assemblies.Contains(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[PolarisCore] Failed to load component assembly {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            foreach (Assembly assembly in assemblies.Where(a => a != core))
            {
                foreach (Type type in SafeTypes(assembly))
                {
                    if (type.IsAbstract || !typeof(PolarisComponent).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    try
                    {
                        components.Add((PolarisComponent)Activator.CreateInstance(type));
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"[PolarisCore] Failed to create component {type.FullName}: {ex.Message}");
                    }
                }
            }

            components.Sort((left, right) =>
            {
                int byOrder = left.Order.CompareTo(right.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(left.Id, right.Id);
            });

            SelectOverrides(nameof(PolarisComponent.Bootstrap), bootstrapComponents);
            SelectOverrides(nameof(PolarisComponent.Awake), awakeComponents);
            SelectOverrides(nameof(PolarisComponent.Start), startComponents);
            SelectOverrides(nameof(PolarisComponent.Update), updateComponents);
            SelectOverrides(nameof(PolarisComponent.LateUpdate), lateUpdateComponents);
            SelectOverrides(nameof(PolarisComponent.Shutdown), shutdownComponents);

            Plugin.Logger.LogMessage($"[PolarisCore] Loaded {components.Count} component(s): {string.Join(", ", components.Select(x => x.Id))}");
        }

        internal static void Bootstrap() => Run(bootstrapComponents, "Bootstrap", component => component.Bootstrap());

        internal static void Awake() => Run(awakeComponents, "Awake", component => component.Awake());

        internal static void Start() => Run(startComponents, "Start", component => component.Start());

        internal static void Update() => Run(updateComponents, "Update", component => component.Update());

        internal static void LateUpdate() => Run(lateUpdateComponents, "LateUpdate", component => component.LateUpdate());

        internal static void Shutdown()
        {
            for (int i = shutdownComponents.Count - 1; i >= 0; i--)
            {
                PolarisComponent component = shutdownComponents[i];
                RunOne(component, "Shutdown", component.Shutdown);
            }
        }

        static void Run(IEnumerable<PolarisComponent> phaseComponents, string phase, Action<PolarisComponent> action)
        {
            foreach (PolarisComponent component in phaseComponents)
            {
                RunOne(component, phase, () => action(component));
            }
        }

        static void RunOne(PolarisComponent component, string phase, Action action)
        {
            try
            {
                using (Diagnostics.DiagnosticsHost.Activity($"component {component.Id} {phase}", component.GetType().Assembly))
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"component {component.Id} {phase}", component.GetType().Assembly);
            }
        }

        static void SelectOverrides(string methodName, ICollection<PolarisComponent> destination)
        {
            foreach (PolarisComponent component in components)
            {
                MethodInfo method = component.GetType().GetMethod(methodName);
                if (method?.DeclaringType != typeof(PolarisComponent))
                {
                    destination.Add(component);
                }
            }
        }

        static void Collect(string directory, ICollection<string> files)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(directory, "Polaris*.dll", SearchOption.TopDirectoryOnly))
            {
                files.Add(file);
            }
        }

        static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }
    }
}
