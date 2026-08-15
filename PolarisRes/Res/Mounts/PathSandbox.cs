using System;
using System.IO;

namespace Polaris.Res.Mounts
{
    /// <summary>路径逃逸校验：对拼接后的路径跑 <see cref="Path.GetFullPath(string)"/>，再校验结果确实在挂载根目录内。</summary>
    internal static class PathSandbox
    {
        /// <summary>返回规范化后的绝对路径；若 <paramref name="combinedPath"/> 逃出 <paramref name="root"/> 或路径非法，返回 null。</summary>
        internal static string Sanitize(string root, string combinedPath)
        {
            string rootFull;
            string full;
            try
            {
                rootFull = Path.GetFullPath(root);
                full = Path.GetFullPath(combinedPath);
            }
            catch
            {
                return null;
            }

            string rootWithSep = rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
            {
                return full;
            }

            return null;
        }
    }
}
