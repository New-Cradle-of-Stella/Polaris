using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Polaris.PUI.HotReload;
using Polaris.PUI.Wire;
using UnityEngine;

namespace Polaris.PUI
{
    /// <summary>
    /// PUI 运行时的根节点持有者 + 类型/图目录 + 便捷封装。自 PUI/PUISolution 改为可直接
    /// 通过 API 实例化的对象之后，本类不再是唯一入口：<see cref="PUIRuntime.Create"/> 与
    /// <see cref="PUIGraphDefinition.CreateSolution"/> 才是核心创建路径，本类只负责
    /// <see cref="Root"/> 的生命周期、按名字的进程级共享实例（供 MainMenuPUI 等非图场景使用）、
    /// 图定义/默认共享解决方案的目录，以及热重载的扇出。
    /// </summary>
    internal static class PUIManager
    {
        /// <summary>按名字的进程级共享 PUI 实例——非图场景（比如 MainMenuPUI 直接按名字打开一个
        /// 窗口）使用；图节点各自创建独立实例，不占用这个表。</summary>
        private static readonly Dictionary<string, PUIRuntime> namedInstances = new Dictionary<string, PUIRuntime>();

        /// <summary>PuiName -&gt; 类型的目录，来自 [PUIAutoRegistration] 扫描结果；供
        /// <see cref="CreateInstance"/>／<see cref="PUIGraphDefinition.Validate"/> 解析图节点。</summary>
        private static readonly Dictionary<string, Type> puiTypes = new Dictionary<string, Type>();

        private static readonly Dictionary<string, PUIGraphDefinition> graphCatalog = new Dictionary<string, PUIGraphDefinition>();

        /// <summary>Init() 时为每份发现的图自动创建的默认共享实例，保留"编译完 .puisln 就能用"
        /// 的零代码体验；需要额外独立实例的 mod 自行再调用 Definition.CreateSolution()。</summary>
        private static readonly Dictionary<string, PUISolution> defaultSolutions = new Dictionary<string, PUISolution>();

        /// <summary>所有存活的、支持热重载的实例（不管是按名字共享的还是某个图节点专属的），供
        /// <see cref="ApplyHotReload"/> 按名字扇出。</summary>
        private static readonly List<PUIHotReloadRuntime> hotReloadInstances = new List<PUIHotReloadRuntime>();

        /// <summary>每个程序集是否标了 <see cref="PUIHotFixEnabledAttribute"/>，只需要判定一次。</summary>
        private static readonly Dictionary<Assembly, bool> hotReloadEnabledAssemblies = new Dictionary<Assembly, bool>();

        private static bool hotReloadServerStarted;

        private static bool initialized;

        /// <summary>所有 PUI 专属 GameObject 的挂载根节点。</summary>
        internal static GameObject Root { get; private set; }

        /// <summary>
        /// 初始化：创建根节点、挂载 <see cref="PUISolutionPump"/>；扫描并注册所有标记了
        /// <see cref="PUIAutoRegistrationAttribute"/> 的 <see cref="IPUI"/> 实现（同时建立
        /// PuiName -&gt; 类型目录）；再扫描所有标记了 <see cref="PUISolutionAutoRegistrationAttribute"/>
        /// 的图类，登记其 <c>Definition</c> 并各自创建一份默认共享 <see cref="PUISolution"/>。
        /// </summary>
        internal static void Init()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            Root = new GameObject("Polaris.PUI.Root");
            UnityEngine.Object.DontDestroyOnLoad(Root);
            PUISolutionPump.EnsureInstance(Root);

            foreach (IPUI pui in DiscoverAutoRegistered())
            {
                // 一个 Mod 的 IPUI 实现或 Register（比如撞名）写坏，不该连累其它 Mod 的自动
                // 注册——尤其是这里是遍历中间，异常不接住会直接中止整个 Init()，后面排队的
                // PUI 类型和下面的 .puisln 状态机图扫描全部不会跑，连最后一行统计日志都出不来。
                try
                {
                    puiTypes[pui.Name] = pui.GetType();
                    Register(pui);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisUI] 自动注册 PUI {pui.GetType().FullName} 失败，已跳过：{ex}");
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
                    Plugin.Logger.LogError($"[PolarisUI] 注册 PUI 状态机图 {graphType.FullName} 失败，已跳过：{ex}");
                }
            }

            // 玩家中途切语言时，已经构建过的 PUI 得重新取一遍词（&key 是在 BuildUI 里求值的），
            // 详见 PUIRuntime.RefreshAllForLocaleChange。
            PolarisAPI.Game.LocaleChanged += OnLocaleChanged;

            Plugin.Logger.LogMessage(
                $"[PolarisUI] 注册了 {puiTypes.Count} 个 PUI、{graphCatalog.Count} 张 PUI 状态机图。");
        }

        private static void OnLocaleChanged(string locale)
        {
            int affected = PUIRuntime.RefreshAllForLocaleChange();
            if (affected > 0)
            {
                Plugin.Logger.LogMessage($"[PolarisUI] 语言切到 {locale}，已刷新 {affected} 个 PUI 的文案。");
            }
        }

        /// <summary>
        /// 手动注册一个 PUI 实例为按名字的进程级共享实例（自动注册未覆盖的场景可用）。
        /// 是否启用热重载由 <paramref name="pui"/> 所在程序集是否标了
        /// <see cref="PUIHotFixEnabledAttribute"/> 决定（见 <see cref="PUIRuntime.Create"/>）。
        /// </summary>
        internal static PUIRuntime Register(IPUI pui)
        {
            if (pui == null)
            {
                throw new ArgumentNullException(nameof(pui));
            }

            if (namedInstances.ContainsKey(pui.Name))
            {
                throw new ArgumentException($"重复的 pui name：{pui.Name}", nameof(pui));
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
                throw new ArgumentException($"未注册的 PUI 名称：{name}", nameof(name));
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
                throw new ArgumentException($"未注册的 PUI 状态机（图）名称：{graphName}", nameof(graphName));
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

        /// <summary>按类型目录新建一份独立的 IPUI + PUIRuntime；不进入 <see cref="namedInstances"/>，
        /// 专供 <see cref="PUISolution"/> 的图节点使用——这正是"真正的多实例"的落地点：每次调用
        /// 都是全新的对象。</summary>
        internal static PUIRuntime CreateInstance(string puiName)
        {
            if (!puiTypes.TryGetValue(puiName, out Type type))
            {
                throw new ArgumentException(
                    $"未知的 PUI 名称：{puiName}（未标 [PUIAutoRegistration] 或所在程序集未加载）", nameof(puiName));
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

        /// <summary>
        /// 把一份热重载指令应用到所有存活的、名字匹配的实例上（一个名字下可能同时存在按名字共享
        /// 的那一份，以及若干个 PUISolution 图节点专属的独立副本，全部收到同一次推送）；由
        /// <see cref="PuiHotReloadPump"/> 在主线程调用。
        /// </summary>
        internal static (bool ok, string error) ApplyHotReload(string name, List<PuiWireCommand> commands)
        {
            List<PUIHotReloadRuntime> targets = hotReloadInstances
                .Where(r => r.State != PUIState.Destroyed && r.Handler.Name == name)
                .ToList();

            if (targets.Count == 0)
            {
                bool knownName = namedInstances.ContainsKey(name) || puiTypes.ContainsKey(name);
                return (false, knownName
                    ? $"「{name}」所属插件未启用 PUIHotFixEnabled，不支持热重载"
                    : $"PUI 未注册：{name}");
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

            return failures.Count == 0 ? (true, null) : (false, string.Join("；", failures));
        }

        // 作用域刻意用 InAppDomain 而不是 InPlugins：PUI 实现类不一定住在 BepInEx 插件
        // 主程序集里，模组把它们拆到附属 dll 是允许的。扫描本身与兜底逻辑走
        // PolarisAPI.Types，程序集的类型表在那里按程序集缓存，各模块共用。
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
                    Plugin.Logger.LogError($"[PolarisUI] 构造自动注册的 PUI 类型 {type.FullName} 失败，已跳过：{ex}");
                    continue;
                }

                yield return instance;
            }
        }

        /// <summary>
        /// 找到所有 .puisln 生成的、带 <see cref="PUISolutionAutoRegistrationAttribute"/> 的
        /// 静态图类（形如 {{FileName}}_Solution）；调用方按需反射读取其 <c>Definition</c>。
        /// </summary>
        private static IEnumerable<Type> DiscoverSolutionGraphs() =>
            PolarisAPI.Types.InAppDomainWith<PUISolutionAutoRegistrationAttribute>().Select(x => x.Type);
    }
}
