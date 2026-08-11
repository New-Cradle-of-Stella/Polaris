using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 程序集 → <see cref="AssemblyOwner"/> 的归属表，整套错误分析的地基。
    /// <para>
    /// <b>判定按路径优先、名字兜底。</b>路径是事实：一个 dll 躺在游戏的 Managed 目录里，
    /// 它就是随游戏分发的；躺在 plugins 里，它就是玩家自己装的。程序集名则是任人填写的元数据，
    /// 撞名、伪装、改名都可能发生，只配当最后一档兜底。
    /// </para>
    /// <para>
    /// 结果永久缓存：程序集一旦加载，它是谁的就不会再变（同 <see cref="Infra.TypesAPI"/> 的
    /// 类型表缓存）。唯一会变的是 BepInEx 插件表，但它在 Chainloader 跑完之后也就定型了，
    /// 需要时用 <see cref="Invalidate"/> 丢弃重建。
    /// </para>
    /// </summary>
    internal static class AssemblyOwnerIndex
    {
        static readonly Dictionary<Assembly, AssemblyOwner> byAssembly =
            new Dictionary<Assembly, AssemblyOwner>();

        static Dictionary<Assembly, PluginInfo> pluginByAssembly;
        static Dictionary<string, AssemblyOwner> byNamespace;

        /// <summary>全部判不出来的帧共用一个实例，省得每帧都 new 一个。</summary>
        static readonly AssemblyOwner UnknownOwner = new AssemblyOwner
        {
            Kind = OwnerKind.Unknown,
            DisplayName = "unknown",
        };

        // ================== 对外查询 ==================

        /// <summary>取一个程序集的归属。<paramref name="assembly"/> 为 null 时给出"未知"。</summary>
        internal static AssemblyOwner Of(Assembly assembly)
        {
            if (assembly == null)
            {
                return UnknownOwner;
            }

            if (byAssembly.TryGetValue(assembly, out AssemblyOwner cached))
            {
                return cached;
            }

            AssemblyOwner owner = Classify(assembly);
            byAssembly[assembly] = owner;
            return owner;
        }

        /// <summary>
        /// 按类型全名取归属，供字符串堆栈（<c>Application.logMessageReceived</c> 只给字符串，
        /// 没有 <see cref="Exception"/> 对象）使用。逐段剥掉命名空间往上找，全都找不到给"未知"。
        /// </summary>
        internal static AssemblyOwner OfTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return UnknownOwner;
            }

            Dictionary<string, AssemblyOwner> map = NamespaceMap();

            // "nel.title.SceneTitleTemp+STATE.foo" → "nel.title.SceneTitleTemp+STATE"
            //   → "nel.title.SceneTitleTemp" → "nel.title" 命中。
            string probe = fullTypeName;
            while (true)
            {
                int dot = probe.LastIndexOf('.');
                if (dot <= 0)
                {
                    return UnknownOwner;
                }

                probe = probe.Substring(0, dot);
                if (map.TryGetValue(probe, out AssemblyOwner owner))
                {
                    // 命名空间被多个归属不同的程序集共用时存的是 null（见 NamespaceMap），
                    // 这种情况不猜，继续往上剥反而会得到更不准的结果，直接认输。
                    return owner ?? UnknownOwner;
                }
            }
        }

        /// <summary>
        /// 按 BepInEx 插件 GUID 取归属。Harmony 的 <c>Patch.owner</c> 按约定就是插件 GUID，
        /// 补丁嫌疑扫描（<see cref="PatchSuspects"/>）靠这个把 owner 字符串换成责任人。
        /// </summary>
        internal static AssemblyOwner ByPluginGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return UnknownOwner;
            }

            foreach (KeyValuePair<Assembly, PluginInfo> pair in PluginMap())
            {
                if (string.Equals(pair.Value.Metadata?.GUID, guid, StringComparison.Ordinal))
                {
                    return Of(pair.Key);
                }
            }

            return UnknownOwner;
        }

        /// <summary>本次游戏加载的全部模组（含 Polaris 自己），供报告头部列清单。</summary>
        internal static IEnumerable<AssemblyOwner> LoadedMods()
            => PluginMap().Keys.Select(Of).Where(o => o.Kind != OwnerKind.Unknown)
                          .OrderBy(o => o.Kind).ThenBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase);

        /// <summary>丢弃缓存。插件表在 Chainloader 跑完前是不完整的，那之前建的表要作废。</summary>
        internal static void Invalidate()
        {
            byAssembly.Clear();
            pluginByAssembly = null;
            byNamespace = null;
        }

        // ================== 判定 ==================

        static AssemblyOwner Classify(Assembly assembly)
        {
            var owner = new AssemblyOwner
            {
                Assembly = assembly,
                DisplayName = SafeName(assembly),
            };

            // 1. 自己。放在最前面，不依赖任何路径推断——Polaris.dll 装在 plugins 根下
            //    还是 plugins/Polaris/ 下由分发方式决定，不该影响"这是我自己"的判断。
            if (assembly == typeof(Plugin).Assembly)
            {
                owner.Kind = OwnerKind.Polaris;
                owner.PluginGuid = MyPluginInfo.PLUGIN_GUID;
                Locate(owner, SafeLocation(assembly));
                return owner;
            }

            string location = SafeLocation(assembly);

            // 2. 没有落盘位置：Harmony 的 DMD、Emit 出来的动态程序集。
            if (string.IsNullOrEmpty(location))
            {
                owner.Kind = OwnerKind.Dynamic;
                return owner;
            }

            Locate(owner, location);

            // 3. 游戏自己的 Managed 目录：原版本体 + 随包第三方 + Unity 引擎 + BCL。
            //    引擎与 BCL 要再挑出来单列，它们永远不该出现在"责任人"和堆栈标注里。
            if (IsUnder(location, ManagedDir))
            {
                owner.Kind = IsRuntimeName(owner.DisplayName) ? OwnerKind.Runtime : OwnerKind.Vanilla;
                return owner;
            }

            // 4. BepInEx 自己的 core 目录：加载器与补丁框架。
            if (IsUnder(location, CoreDir))
            {
                owner.Kind = OwnerKind.Framework;
                return owner;
            }

            // 5. Polaris 随包分发的第三方依赖。必须排在下面的 plugins 通用判断之前——
            //    这个目录本来就在 plugins 底下，顺序反了会被认成普通模组。
            if (IsUnder(location, PolarisAPI.Paths.LibsDir))
            {
                owner.Kind = OwnerKind.ModLibrary;
                return owner;
            }

            // 6. plugins 底下的其它 dll：是 BepInEx 插件才算"模组"，否则只是模组随包的依赖。
            if (IsUnder(location, PolarisAPI.Paths.PluginsRoot))
            {
                owner.Kind = PluginMap().ContainsKey(assembly) ? OwnerKind.Mod : OwnerKind.ModLibrary;
                Enrich(owner);
                return owner;
            }

            // 7. 名字兜底。走到这里说明 dll 在上面任何一个约定目录之外（玩家手动挪过、
            //    或者是 GAC / Mono 自带的东西），只能靠名字猜个大类。
            owner.Kind = ClassifyByName(owner.DisplayName);
            return owner;
        }

        /// <summary>填 <see cref="AssemblyOwner.FileName"/> / <see cref="AssemblyOwner.FullPath"/>。</summary>
        static void Locate(AssemblyOwner owner, string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return;
            }

            owner.FullPath = location;
            owner.FileName = Path.GetFileName(location);
        }

        /// <summary>
        /// 给模组类归属补上 GUID 与 <see cref="PolarisModInfo"/>（作者、主页）——报告尾部
        /// "该找谁"那一段全靠这些字段，没有它们归因结论就只是一个 dll 名字。
        /// </summary>
        static void Enrich(AssemblyOwner owner)
        {
            if (owner.FileName != null)
            {
                owner.ModInfo = PolarisModInfoResolver.Resolve(owner.FileName);
                if (!string.IsNullOrEmpty(owner.ModInfo?.DisplayName))
                {
                    owner.DisplayName = owner.ModInfo.DisplayName;
                }
            }

            if (owner.Assembly != null && PluginMap().TryGetValue(owner.Assembly, out PluginInfo info))
            {
                owner.PluginGuid = info.Metadata?.GUID;
            }
        }

        static OwnerKind ClassifyByName(string name)
        {
            if (IsRuntimeName(name))
            {
                return OwnerKind.Runtime;
            }

            if (StartsWithAny(name, "BepInEx", "0Harmony", "HarmonyLib", "MonoMod", "Mono.Cecil", "SemanticVersioning"))
            {
                return OwnerKind.Framework;
            }

            return OwnerKind.Unknown;
        }

        static bool IsRuntimeName(string name)
            => StartsWithAny(name,
                "mscorlib", "netstandard", "System", "Microsoft", "Mono.", "I18N",
                "UnityEngine", "Unity.", "TextMeshPro", "TMPro");

        static bool StartsWithAny(string value, params string[] prefixes)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (string prefix in prefixes)
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // ================== 反查表 ==================

        /// <summary>
        /// <c>Assembly → PluginInfo</c>。BepInEx 只给了 GUID → PluginInfo 的正向表，
        /// 而归因是从堆栈帧（也就是程序集）出发的，必须有这张反向表。
        /// </summary>
        static Dictionary<Assembly, PluginInfo> PluginMap()
        {
            if (pluginByAssembly != null)
            {
                return pluginByAssembly;
            }

            var map = new Dictionary<Assembly, PluginInfo>();

            foreach (PluginInfo info in PolarisAPI.Modules.Plugins)
            {
                Assembly assembly = SafeAssemblyOf(info);
                if (assembly != null && !map.ContainsKey(assembly))
                {
                    map[assembly] = info;
                }
            }

            pluginByAssembly = map;
            return map;
        }

        /// <summary>
        /// 命名空间 → 归属。只在真的需要解析字符串堆栈时才建，代价是把每个已加载程序集的
        /// 类型表读一遍（<see cref="Infra.TypesAPI.Of"/> 本身带缓存，Assembly-CSharp 那 5MB
        /// 也只解析这一次）。这笔开销发生在"已经出错了"的路径上，换来的是每一帧都能标出归属，
        /// 值得。
        /// <para>
        /// 同一个命名空间被归属不同的程序集共用时存 null（视作"说不清"）：模组用
        /// <c>Polaris.XXX</c> 之类的命名空间并不罕见，把这种帧算到 Polaris 头上就是冤案。
        /// </para>
        /// </summary>
        static Dictionary<string, AssemblyOwner> NamespaceMap()
        {
            if (byNamespace != null)
            {
                return byNamespace;
            }

            var map = new Dictionary<string, AssemblyOwner>(StringComparer.Ordinal);

            foreach (Assembly assembly in SafeLoadedAssemblies())
            {
                AssemblyOwner owner = Of(assembly);
                if (owner.Kind == OwnerKind.Unknown || owner.Kind == OwnerKind.Dynamic)
                {
                    continue;
                }

                foreach (Type type in PolarisAPI.Types.Of(assembly))
                {
                    string ns = type.Namespace;
                    if (string.IsNullOrEmpty(ns))
                    {
                        continue;
                    }

                    if (!map.TryGetValue(ns, out AssemblyOwner existing))
                    {
                        map[ns] = owner;
                        continue;
                    }

                    // 已经被别人占了：同一个归属无所谓，不同归属就作废这个命名空间。
                    if (existing != null && existing.Kind != owner.Kind)
                    {
                        map[ns] = null;
                    }
                }
            }

            byNamespace = map;
            return map;
        }

        // ================== 兜底的反射访问 ==================
        // 下面这些 try/catch 不是防御性洁癖：错误分析跑在"已经出事了"的路径上，
        // 此刻 AppDomain 里很可能正躺着几个加载了一半的程序集，读 Location 抛
        // NotSupportedException 是常事。这里再抛一次异常就会盖掉原始错误。

        static string SafeLocation(Assembly assembly)
        {
            try
            {
                return assembly.IsDynamic ? null : assembly.Location;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string SafeName(Assembly assembly)
        {
            try
            {
                return assembly.GetName().Name;
            }
            catch (Exception)
            {
                return assembly.FullName;
            }
        }

        static Assembly SafeAssemblyOf(PluginInfo info)
        {
            try
            {
                return info?.Instance?.GetType().Assembly;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static IEnumerable<Assembly> SafeLoadedAssemblies()
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies();
            }
            catch (Exception)
            {
                return Enumerable.Empty<Assembly>();
            }
        }

        // ================== 目录常量 ==================

        static string managedDir;
        static string coreDir;

        /// <summary>游戏自带程序集目录，例如 <c>…\AliceInCradle_Data\Managed\</c>。</summary>
        static string ManagedDir => managedDir ??= SafePath(() => Paths.ManagedPath);

        /// <summary>BepInEx 自身所在目录 <c>BepInEx\core\</c>。</summary>
        static string CoreDir => coreDir ??= SafePath(() => Paths.BepInExAssemblyDirectory);

        static string SafePath(Func<string> get)
        {
            try
            {
                return get();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// <paramref name="path"/> 是否在 <paramref name="directory"/> 之下（含任意层子目录）。
        /// 补上末尾分隔符再比，否则 <c>plugins</c> 会把 <c>plugins_backup</c> 也算进来。
        /// </summary>
        static bool IsUnder(string path, string directory)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
            {
                return false;
            }

            try
            {
                string full = Path.GetFullPath(directory);
                if (full[full.Length - 1] != Path.DirectorySeparatorChar)
                {
                    full += Path.DirectorySeparatorChar;
                }

                return Path.GetFullPath(path).StartsWith(full, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
