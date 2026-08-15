using System;
using System.Collections.Generic;
using System.IO;

namespace Polaris.Res.Mounts
{
    /// <summary>单个模组的挂载列表 + 解析算法。属于 <c>ModResources</c>，每个模组一份。</summary>
    internal sealed class MountTable
    {
        /// <summary>不做扩展名探测时用的唯一候选后缀（<see cref="ResourceId.Path"/> 原样探测）。</summary>
        private static readonly string[] NoSuffix = { "" };

        private readonly List<DirectoryMount> mounts = new List<DirectoryMount>();
        private int nextRegistrationOrder;

        internal IReadOnlyList<DirectoryMount> Mounts => mounts;

        /// <summary>按 <see cref="DirectoryMount.Priority"/> 降序、同优先级按注册顺序降序排列（后注册的赢）。</summary>
        /// <remarks>同一物理目录重复挂载是幂等的，直接复用已有条目，避免诊断信息里重复列出同一目录。</remarks>
        internal DirectoryMount Add(string absoluteRoot, int priority)
        {
            // 路径非法时这里就抛；DirectoryMount 的构造函数同样要规范化一次，抛的是同一个异常。
            string fullPath = Path.GetFullPath(absoluteRoot);

            foreach (DirectoryMount existing in mounts)
            {
                if (string.Equals(existing.RootPath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            DirectoryMount mount = new DirectoryMount(absoluteRoot, priority, nextRegistrationOrder++);
            mounts.Add(mount);
            mounts.Sort(CompareMounts);
            return mount;
        }

        private static int CompareMounts(DirectoryMount a, DirectoryMount b)
        {
            int byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : b.RegistrationOrder.CompareTo(a.RegistrationOrder);
        }

        /// <summary>挂载优先、扩展名次之，命中即停；未命中时 <paramref name="probeLog"/> 记录了所有尝试过的候选。</summary>
        internal bool TryResolve(ResourceId id, out string absolutePath, out MountProbeLog probeLog) =>
            TryResolve(id, out absolutePath, out _, out probeLog);

        /// <summary>同上，另外带出命中的挂载根目录，供 <see cref="Import.ImportMetaResolver"/> 逐层查找 <c>_import.json</c> 用。</summary>
        internal bool TryResolve(ResourceId id, out string absolutePath, out string mountRoot, out MountProbeLog probeLog)
        {
            probeLog = new MountProbeLog(id);
            IReadOnlyList<string> suffixes = BuildCandidateSuffixes(id);

            foreach (DirectoryMount mount in mounts)
            {
                probeLog.BeginMount(mount.RootPath, mount.Priority);

                foreach (string suffix in suffixes)
                {
                    string relative = id.Path + suffix;

                    if (mount.TryResolveExact(relative, out string exact))
                    {
                        absolutePath = exact;
                        mountRoot = mount.RootPath;
                        return true;
                    }

                    probeLog.RecordMiss(relative);

                    if (mount.TryResolveCaseInsensitive(relative, out string caseInsensitive, out string actualCasing))
                    {
                        probeLog.RecordCaseMismatch(relative, actualCasing, mount.RootPath);
                        Plugin.Logger.LogWarning(
                            $"[PolarisRes] {id} matched a file with inconsistent casing: expected \"{relative}\", " +
                            $"found \"{actualCasing}\" (mount {mount.RootPath}). Making the casing consistent is recommended.");
                        absolutePath = caseInsensitive;
                        mountRoot = mount.RootPath;
                        return true;
                    }
                }
            }

            absolutePath = null;
            mountRoot = null;
            return false;
        }

        /// <summary>若 <see cref="ResourceId.Path"/> 已带该 Kind 的候选扩展名，原样探测；否则依次尝试每个候选扩展名。</summary>
        private static IReadOnlyList<string> BuildCandidateSuffixes(ResourceId id)
        {
            IReadOnlyList<string> extensions = id.Kind.CandidateExtensions();
            if (extensions.Count == 0)
            {
                return NoSuffix;
            }

            foreach (string ext in extensions)
            {
                if (id.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return NoSuffix;
                }
            }

            return extensions;
        }
    }
}
