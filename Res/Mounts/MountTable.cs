using System;
using System.Collections.Generic;
using System.IO;

namespace Polaris.Res.Mounts
{
    /// <summary>单个模组的挂载列表 + 解析算法。属于 <c>ModResources</c>，每个模组一份。</summary>
    internal sealed class MountTable
    {
        private readonly List<DirectoryMount> mounts = new List<DirectoryMount>();
        private int nextRegistrationOrder;

        /// <summary>
        /// 按 <see cref="DirectoryMount.Priority"/> 降序、同优先级按注册顺序降序排列——
        /// 后注册的赢，所以开发期"额外挂一个源目录、优先级给高一点"的写法里，
        /// 就算两个挂载优先级相同，后写的 <c>Mount(...)</c> 调用也会先被探测到。
        /// </summary>
        /// <remarks>
        /// 同一个物理目录（<see cref="Path.GetFullPath(string)"/> 后大小写不敏感比较）重复挂载
        /// 是幂等的，直接复用已有条目——<c>AutoBindScanner</c> 的自动挂载和模组自己手动调用
        /// <c>MountDefault()</c> 算出来的是同一个目录，不应该真的挂两份，否则"找不到资源"的
        /// 诊断信息里会把同一个目录重复列两次。
        /// </remarks>
        internal DirectoryMount Add(string absoluteRoot, int priority)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(absoluteRoot);
            }
            catch
            {
                fullPath = absoluteRoot;
            }

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

        internal IReadOnlyList<DirectoryMount> Mounts => mounts;

        /// <summary>
        /// 挂载优先、扩展名次之，命中即停。未命中时 <paramref name="probeLog"/> 记录了
        /// 每一个尝试过的候选，供调用方构造 <see cref="ResourceNotFoundException"/> 的消息。
        /// </summary>
        internal bool TryResolve(ResourceId id, out string absolutePath, out MountProbeLog probeLog) =>
            TryResolve(id, out absolutePath, out _, out probeLog);

        /// <summary>
        /// 同上，另外带出命中的那个挂载的根目录——<see cref="Import.ImportMetaResolver"/>
        /// 需要知道"从哪个根开始逐层找 <c>_import.json</c>"，不能拿任意一个挂载的根来用。
        /// </summary>
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

                    probeLog.RecordProbe(relative, exists: false);

                    if (mount.TryResolveCaseInsensitive(relative, out string caseInsensitive, out string actualCasing))
                    {
                        probeLog.RecordCaseMismatch(relative, actualCasing, mount.RootPath);
                        Plugin.Logger.LogWarning(
                            $"[PolarisRes] {id} 命中大小写不一致的文件：期望 \"{relative}\"，" +
                            $"实际 \"{actualCasing}\"（挂载 {mount.RootPath}）。建议统一大小写。");
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

        /// <summary>
        /// 如果 <see cref="ResourceId.Path"/> 已经以该 Kind 的某个候选扩展名结尾，只把它当唯一候选
        /// （原样探测，不重复拼接扩展名）；否则依次尝试每个候选扩展名。
        /// </summary>
        private static IReadOnlyList<string> BuildCandidateSuffixes(ResourceId id)
        {
            IReadOnlyList<string> extensions = id.Kind.CandidateExtensions();
            if (extensions.Count == 0)
            {
                return new[] { "" };
            }

            foreach (string ext in extensions)
            {
                if (id.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { "" };
                }
            }

            return extensions;
        }
    }
}
