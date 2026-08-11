using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 对堆栈里的原版方法反查"它被谁改过"。
    /// <para>
    /// <b>这是整套归因里最要紧的一步。</b>Harmony 的 transpiler 把 IL 直接织进原版方法体，
    /// 抛出来的异常在堆栈上看起来 100% 是原版自己的——一个模组帧都没有。只靠走栈，
    /// 所有 transpiler 引发的崩溃都会被判成"原版游戏的问题"，然后玩家拿着这个结论去找
    /// 游戏作者，而这正是 <see cref="PolarisModWarning"/> 那一页拼命想避免的事。
    /// （Polaris 自己就有两个 transpiler，见 <c>Patch_SceneTitleTemp_initButtons</c> 与
    /// <c>Patch_UiGameMenu_remakeLeftCategories</c>，所以这不是假想的风险。）
    /// </para>
    /// <para>
    /// prefix/postfix 通常会留下自己的帧，但内联、委托、以及 Harmony 内部的调度都可能让它丢失，
    /// 所以这里对四类补丁一视同仁地全查一遍。
    /// </para>
    /// </summary>
    internal static class PatchSuspects
    {
        /// <summary>单个方法的补丁扫描结果。</summary>
        internal sealed class Scan
        {
            internal List<ErrorSuspect> Suspects { get; } = new List<ErrorSuspect>();

            /// <summary>写在这一帧后面的说明，例如 <c>被「XXX」以 transpiler 改写</c>。没补丁时为 null。</summary>
            internal string Note { get; set; }

            /// <summary>是否有 transpiler / IL 改写参与——这类补丁不留堆栈帧，结论要额外谨慎。</summary>
            internal bool HasIlRewrite { get; set; }

            internal bool Any => Suspects.Count > 0;
        }

        static readonly Scan Empty = new Scan();

        /// <summary>
        /// 查 <paramref name="method"/> 被哪些模组打了补丁。<paramref name="methodDisplay"/>
        /// 只用于拼给人看的理由文本。
        /// </summary>
        internal static Scan Of(MethodBase method, string methodDisplay)
        {
            if (method == null)
            {
                return Empty;
            }

            Patches patches;
            try
            {
                patches = Harmony.GetPatchInfo(method);
            }
            catch (Exception)
            {
                // Harmony 内部状态在崩溃现场未必健康。查不到补丁只是少一条线索，
                // 绝不能因此把正在分析的那个错误也弄丢。
                return Empty;
            }

            if (patches == null)
            {
                return Empty;
            }

            var scan = new Scan();
            var byOwner = new Dictionary<AssemblyOwner, List<string>>();

            Collect(patches.Prefixes, "prefix", byOwner, scan);
            Collect(patches.Postfixes, "postfix", byOwner, scan);
            Collect(patches.Finalizers, "finalizer", byOwner, scan);
            Collect(patches.Transpilers, "transpiler", byOwner, scan);
            Collect(patches.ILManipulators, "IL rewrite", byOwner, scan);

            if (byOwner.Count == 0)
            {
                return Empty;
            }

            foreach (KeyValuePair<AssemblyOwner, List<string>> pair in byOwner)
            {
                string kinds = string.Join("/", pair.Value.Distinct().ToArray());
                bool invisible = pair.Value.Any(k => k == "transpiler" || k == "IL rewrite");

                scan.Suspects.Add(new ErrorSuspect
                {
                    Owner = pair.Key,
                    Reason = invisible
                        ? $"rewrote {methodDisplay} via {kinds} (an IL rewrite leaves no frame of its own in the stack)"
                        : $"modified {methodDisplay} via {kinds}",
                });
            }

            // transpiler 排前面：它是唯一"堆栈上完全隐形"的那种，最需要被看见。
            scan.Suspects.Sort((a, b) => Rank(b).CompareTo(Rank(a)));

            scan.Note = "modified by " + string.Join(", ", scan.Suspects
                .Select(s => $"\"{s.Owner.DisplayName}\"").ToArray());

            return scan;
        }

        static int Rank(ErrorSuspect suspect)
            => suspect.Reason != null && suspect.Reason.Contains("IL rewrite") ? 1 : 0;

        /// <summary>
        /// 只有类型名和方法名（字符串堆栈那一路，没有 <see cref="MethodBase"/>）时，
        /// 从 Harmony 已打补丁的方法表里按名字反找。
        /// <para>
        /// 不缓存：这张表通常只有几十条（就是本局所有模组打的补丁总数），而缓存反而要处理
        /// "模组在运行中途才打补丁"的失效问题。何况这个方法只在新指纹的事件首次分析时才被调到。
        /// </para>
        /// </summary>
        internal static MethodBase FindPatched(string typeName, string methodName)
        {
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            try
            {
                foreach (MethodBase method in Harmony.GetAllPatchedMethods())
                {
                    if (string.Equals(method?.Name, methodName, StringComparison.Ordinal)
                        && string.Equals(method.DeclaringType?.FullName, typeName, StringComparison.Ordinal))
                    {
                        return method;
                    }
                }
            }
            catch (Exception)
            {
                // 同 Of()：查不到补丁只是少一条线索。
            }

            return null;
        }

        static void Collect(
            IEnumerable<HarmonyLib.Patch> patches,
            string kind,
            Dictionary<AssemblyOwner, List<string>> byOwner,
            Scan scan)
        {
            if (patches == null)
            {
                return;
            }

            foreach (HarmonyLib.Patch patch in patches)
            {
                AssemblyOwner owner = Resolve(patch);
                if (owner == null || owner.Kind == OwnerKind.Unknown)
                {
                    continue;
                }

                if (!byOwner.TryGetValue(owner, out List<string> kinds))
                {
                    kinds = new List<string>();
                    byOwner[owner] = kinds;
                }

                kinds.Add(kind);

                if (kind == "transpiler" || kind == "IL rewrite")
                {
                    scan.HasIlRewrite = true;
                }
            }
        }

        /// <summary>
        /// 把一条补丁记录换成责任人。优先看补丁方法所在的程序集——那是事实；
        /// 拿不到再退回 <c>Patch.owner</c>（Harmony 实例 id，按约定等于插件 GUID，
        /// 但只是约定，模组完全可以随便填一个字符串）。
        /// </summary>
        static AssemblyOwner Resolve(HarmonyLib.Patch patch)
        {
            try
            {
                Assembly assembly = patch?.PatchMethod?.DeclaringType?.Assembly;
                if (assembly != null)
                {
                    AssemblyOwner owner = AssemblyOwnerIndex.Of(assembly);
                    if (owner.Kind != OwnerKind.Unknown)
                    {
                        return owner;
                    }
                }

                return AssemblyOwnerIndex.ByPluginGuid(patch?.owner);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
