using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Polaris.PUI.HotReload;
using Polaris.PUI.Wire;
using UnityEngine;

namespace Polaris.PUI
{
    /// <summary>PUI 运行时的根节点持有者 + 类型/图目录 + 便捷封装；负责 Root 生命周期、按名字的共享实例、图定义目录及热重载扇出。</summary>
    internal static class PUIManager
    {
        /// <summary>按名字的进程级共享 PUI 实例；图节点各自创建独立实例，不占用此表。</summary>
        private static readonly Dictionary<string, PUIRuntime> namedInstances = new Dictionary<string, PUIRuntime>();

        /// <summary>PuiName -&gt; 类型目录，来自 [PUIAutoRegistration] 扫描结果。</summary>
        private static readonly Dictionary<string, Type> puiTypes = new Dictionary<string, Type>();

        private static readonly Dictionary<string, PUIGraphDefinition> graphCatalog = new Dictionary<string, PUIGraphDefinition>();

        /// <summary>Init() 时为每份发现的图自动创建的默认共享实例。</summary>
        private static readonly Dictionary<string, PUISolution> defaultSolutions = new Dictionary<string, PUISolution>();

        /// <summary>所有存活的支持热重载的实例，供 <see cref="ApplyHotReload"/> 按名字扇出。</summary>
        private static readonly List<PUIHotReloadRuntime> hotReloadInstances = new List<PUIHotReloadRuntime>();

        /// <summary>每个程序集是否启用热重载，只判定一次并缓存。</summary>
        private static readonly Dictionary<Assembly, bool> hotReloadEnabledAssemblies = new Dictionary<Assembly, bool>();

        private static bool hotReloadServerStarted;

        private static bool initialized;

        /// <summary>所有 PUI 专属 GameObject 的挂载根节点。</summary>
        internal static GameObject Root { get; private set; }

        /// <summary>初始化：创建根节点，扫描并注册所有 <see cref="IPUI"/> 自动注册实现与状态机图类，各建立目录并为图创建默认共享解决方案。</summary>
        internal static void Init()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            Root = new GameObject("Polaris.PUI.Root");

            // 设置根节点 z，避免与标题场景里同为 z=0 的版本号文本重叠；取值见 UiDepth。
            XX.IN.setZ(Root.transform, UiDepth.Window);

            UnityEngine.Object.DontDestroyOnLoad(Root);
            PUISolutionPump.EnsureInstance(Root);

            foreach (IPUI pui in DiscoverAutoRegistered())
            {
                // 单个 Mod 的注册失败不应中止整个 Init() 及后续扫描。
                try
                {
                    puiTypes[pui.Name] = pui.GetType();
                    Register(pui);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisUI] Failed to auto-register PUI {pui.GetType().FullName}; skipped: {ex}");
                }
            }

            foreach (Type graphType in DiscoverSolutionGraphs())
            {
                try
                {
                    PropertyInfo prop = graphType.GetProperty("Definition", BindingFlags.Public | BindingFlags.Static);
                    if (prop?.GetValue(null) is PUIGraphDefinition definition)
                    {
                        graphCatalog[definition.Name] = definition;
                        defaultSolutions[definition.Name] = definition.CreateSolution();
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisUI] Failed to register PUI state machine graph {graphType.FullName}; skipped: {ex}");
                }
            }

            // 切语言时需重新取词，详见 PUIRuntime.RefreshAllForLocaleChange。
            API.GameSessionRuntime.LocaleChanged += OnLocaleChanged;

            Plugin.Logger.LogMessage(
                $"[PolarisUI] Registered {puiTypes.Count} PUIs and {graphCatalog.Count} PUI state machine graphs.");
        }

        private static void OnLocaleChanged(string previous, string locale)
        {
            int affected = PUIRuntime.RefreshAllForLocaleChange();
            if (affected > 0)
            {
                Plugin.Logger.LogMessage($"[PolarisUI] Language changed to {locale}; refreshed text for {affected} PUIs.");
            }
        }

        /// <summary>手动注册一个 PUI 实例为按名字的进程级共享实例。</summary>
        internal static PUIRuntime Register(IPUI pui)
        {
            if (pui == null)
            {
                throw new ArgumentNullException(nameof(pui));
            }

            if (namedInstances.ContainsKey(pui.Name))
            {
                throw new ArgumentException($"Duplicate pui name: {pui.Name}", nameof(pui));
            }

            PUIRuntime runtime = PUIRuntime.Create(pui);
            namedInstances[pui.Name] = runtime;
            return runtime;
        }

        /// <summary>查询按名字共享注册的 PUI 运行时实例；未注册则抛出。</summary>
        /// <exception cref="ArgumentException">name 未注册</exception>
        internal static PUIRuntime Get(string name)
        {
            if (!namedInstances.TryGetValue(name, out PUIRuntime runtime))
            {
                throw new ArgumentException($"Unregistered PUI name: {name}", nameof(name));
            }

            return runtime;
        }

        internal static bool TryGet(string name, out PUIRuntime runtime) => namedInstances.TryGetValue(name, out runtime);

        /// <summary>查询指定名称的 pui 是否已按名字注册（自动注册或手动注册）。</summary>
        internal static bool IsRegistered(string name) => namedInstances.ContainsKey(name);

        /// <summary>查询一份已编译 .puisln 图的不可变蓝图。</summary>
        internal static bool TryGetGraph(string graphName, out PUIGraphDefinition definition) =>
            graphCatalog.TryGetValue(graphName, out definition);

        /// <summary>Init() 时为该图自动创建的默认共享 <see cref="PUISolution"/> 实例。</summary>
        /// <exception cref="ArgumentException">graphName 未注册</exception>
        internal static PUISolution GetDefaultSolution(string graphName)
        {
            if (!defaultSolutions.TryGetValue(graphName, out PUISolution solution))
            {
                throw new ArgumentException($"Unregistered PUI state machine (graph) name: {graphName}", nameof(graphName));
            }

            return solution;
        }

        /// <summary>显示指定按名字共享的 pui；若尚未构建会先构建（GetUIWindow + BuildUI），再激活。</summary>
        /// <exception cref="ArgumentException">name 未注册</exception>
        internal static void ShowUI(string name) => Get(name).Show();

        /// <summary>隐藏指定按名字共享的 pui（不销毁，可再次 ShowUI）。</summary>
        /// <exception cref="ArgumentException">name 未注册</exception>
        internal static void HideUI(string name) => Get(name).Hide();

        /// <summary>销毁指定按名字共享的 pui 的运行时对象；销毁后不可再显示。</summary>
        /// <exception cref="ArgumentException">name 未注册</exception>
        internal static void CloseUI(string name) => Get(name).Destroy();

        /// <summary>让指定按名字共享的 pui 抢占引擎输入焦点。</summary>
        /// <exception cref="ArgumentException">name 未注册</exception>
        internal static void FocusUI(string name) => Get(name).Focus();

        /// <summary>查询指定按名字共享的 pui 当前所处的生命周期状态。</summary>
        /// <exception cref="ArgumentException">name 未注册</exception>
        internal static PUIState GetState(string name) => Get(name).State;

        /// <summary>PuiName 是否能在类型目录里解析到；供 <see cref="PUIGraphDefinition.Validate"/> 使用。</summary>
        internal static bool IsKnownPuiName(string puiName) => puiTypes.ContainsKey(puiName);

        /// <summary>按类型目录新建一份独立的 IPUI + PUIRuntime，供 <see cref="PUISolution"/> 图节点使用；不进入 <see cref="namedInstances"/>。</summary>
        internal static PUIRuntime CreateInstance(string puiName)
        {
            if (!puiTypes.TryGetValue(puiName, out Type type))
            {
                throw new ArgumentException(
                    $"Unknown PUI name: {puiName} (not marked [PUIAutoRegistration], or its assembly is not loaded)", nameof(puiName));
            }

            var handler = (IPUI)Activator.CreateInstance(type);
            return PUIRuntime.Create(handler);
        }

        internal static bool IsHotReloadEnabled(Assembly assembly)
        {
            if (hotReloadEnabledAssemblies.TryGetValue(assembly, out bool cached))
            {
                return cached;
            }

            bool enabled = PolarisAPI.Types.Of(assembly)
                .Any(type => type.GetCustomAttribute<PUIHotFixEnabledAttribute>() != null);

            hotReloadEnabledAssemblies[assembly] = enabled;
            return enabled;
        }

        internal static void EnsureHotReloadServerStarted()
        {
            if (hotReloadServerStarted)
            {
                return;
            }

            hotReloadServerStarted = true;
            PuiHotReloadServer.Start(Root);
        }

        /// <summary>由 <see cref="PUIRuntime.Create"/> 在创建热重载实例时调用，登记进扇出表。</summary>
        internal static void TrackHotReload(PUIHotReloadRuntime runtime)
        {
            hotReloadInstances.Add(runtime);
        }

        /// <summary>把一份热重载指令推送给所有存活的、名字匹配的实例（可能同时命中共享实例与多个图节点副本）。</summary>
        internal static (bool ok, string error) ApplyHotReload(string name, List<PuiWireCommand> commands)
        {
            List<PUIHotReloadRuntime> targets = hotReloadInstances
                .Where(r => r.State != PUIState.Destroyed && r.Handler.Name == name)
                .ToList();

            if (targets.Count == 0)
            {
                bool knownName = namedInstances.ContainsKey(name) || puiTypes.ContainsKey(name);
                return (false, knownName
                    ? $"The plugin owning \"{name}\" has not enabled PUIHotFixEnabled; hot reload is unsupported"
                    : $"PUI not registered: {name}");
            }

            var failures = new List<string>();
            foreach (PUIHotReloadRuntime runtime in targets)
            {
                (bool ok, string error) = runtime.ApplyHotReload(commands);
                if (!ok)
                {
                    failures.Add(error);
                }
            }

            return failures.Count == 0 ? (true, null) : (false, string.Join("; ", failures));
        }

        // 用 InAppDomain 而非 InPlugins：PUI 实现类允许拆在附属 dll 里，不局限于主插件程序集。
        private static IEnumerable<IPUI> DiscoverAutoRegistered()
        {
            foreach ((Type type, _) in PolarisAPI.Types.InAppDomainWith<PUIAutoRegistrationAttribute>())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(IPUI).IsAssignableFrom(type))
                {
                    continue;
                }

                IPUI instance;
                try
                {
                    instance = (IPUI)Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisUI] Failed to construct auto-registered PUI type {type.FullName}; skipped: {ex}");
                    continue;
                }

                yield return instance;
            }
        }

        /// <summary>找到所有 .puisln 生成的带 <see cref="PUISolutionAutoRegistrationAttribute"/> 的静态图类。</summary>
        private static IEnumerable<Type> DiscoverSolutionGraphs() =>
            PolarisAPI.Types.InAppDomainWith<PUISolutionAutoRegistrationAttribute>().Select(x => x.Type);
    }
}
