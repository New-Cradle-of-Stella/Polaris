using System;
using System.IO;

namespace Polaris.Res.Mounts
{
    /// <summary>
    /// 路径逃逸校验。原版 <c>NKT.readSpecificStreamingText</c> 只拒绝字面 <c>..</c> 子串，
    /// 那不是真沙箱（比如绝对路径、符号链接、URL 编码都绕得过去）；这里改为对拼接后的
    /// 路径跑 <see cref="Path.GetFullPath(string)"/>，再校验结果确实还在挂载根目录内。
    /// </summary>
    internal static class PathSandbox
    {
        /// <summary>
        /// 返回规范化后的绝对路径；如果 <paramref name="combinedPath"/> 解析后逃出了
        /// <paramref name="root"/>，或者路径本身非法，返回 null。
        /// </summary>
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

            string rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;

            if (full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
            {
                return full;
            }

            return null;
        }
    }
}
