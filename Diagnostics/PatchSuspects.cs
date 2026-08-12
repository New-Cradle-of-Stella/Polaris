using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 对堆栈里的原版方法反查它被谁改过。transpiler/IL 改写不留堆栈帧，只靠走栈会误判成原版问题，
    /// 所以对 prefix/postfix/finalizer/transpiler/IL rewrite 五类补丁一视同仁全查一遍。
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

        /// <summary>查 <paramref name="method"/> 被哪些模组打了补丁；<paramref name="methodDisplay"/> 仅用于展示文本。</summary>
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
                // 查不到补丁只是少一条线索，不能因此丢掉正在分析的错误。
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

            // transpiler 排前面，因为它在堆栈上完全隐形，最需要被看见。
            scan.Suspects.Sort((a, b) => Rank(b).CompareTo(Rank(a)));

            scan.Note = "modified by " + string.Join(", ", scan.Suspects
                .Select(s => $"\"{s.Owner.DisplayName}\"").ToArray());

            return scan;
        }

        static int Rank(ErrorSuspect suspect)
            => suspect.Reason != null && suspect.Reason.Contains("IL rewrite") ? 1 : 0;

        /// <summary>只有类型名/方法名（字符串堆栈）时，从 Harmony 已打补丁的方法表按名字反找；不缓存。</summary>
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
                // 查不到补丁只是少一条线索。
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

        /// <summary>把补丁记录换成责任人：优先看补丁方法所在程序集，拿不到再退回 <c>Patch.owner</c>。</summary>
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
