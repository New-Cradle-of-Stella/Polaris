using System.Collections.Generic;
using System.IO;

namespace Polaris.Res.Mounts
{
    /// <summary>
    /// 一个磁盘根目录。先试精确大小写（快路径，绝大多数情况下一次 <see cref="File.Exists"/>
    /// 就够了），未命中再查惰性建立的大小写不敏感索引。索引在 M8 热重载接入前不会失效，
    /// 因为目前也没有任何东西会在运行期往挂载目录里加文件。
    /// </summary>
    internal sealed class DirectoryMount
    {
        internal string RootPath { get; }
        internal int Priority { get; }
        internal int RegistrationOrder { get; }

        private Dictionary<string, string> lowercaseIndex;
        private bool indexed;

        internal DirectoryMount(string rootPath, int priority, int registrationOrder)
        {
            RootPath = Path.GetFullPath(rootPath);
            Priority = priority;
            RegistrationOrder = registrationOrder;
        }

        /// <summary>精确大小写命中。</summary>
        internal bool TryResolveExact(string relativePath, out string absolutePath)
        {
            string combined = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string sanitized = PathSandbox.Sanitize(RootPath, combined);
            if (sanitized != null && File.Exists(sanitized))
            {
                absolutePath = sanitized;
                return true;
            }

            absolutePath = null;
            return false;
        }

        /// <summary>大小写不敏感兜底；<paramref name="actualRelativeCasing"/> 是磁盘上的真实大小写，用于警告。</summary>
        internal bool TryResolveCaseInsensitive(string relativePath, out string absolutePath, out string actualRelativeCasing)
        {
            EnsureIndexed();

            string key = relativePath.Replace('\\', '/').ToLowerInvariant();
            if (lowercaseIndex.TryGetValue(key, out string actualRelative))
            {
                absolutePath = Path.Combine(RootPath, actualRelative.Replace('/', Path.DirectorySeparatorChar));
                actualRelativeCasing = actualRelative;
                return true;
            }

            absolutePath = null;
            actualRelativeCasing = null;
            return false;
        }

        private void EnsureIndexed()
        {
            if (indexed)
            {
                return;
            }

            indexed = true;
            lowercaseIndex = new Dictionary<string, string>();

            if (!Directory.Exists(RootPath))
            {
                return;
            }

            int rootLength = RootPath.Length;
            foreach (string file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(rootLength)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                lowercaseIndex[relative.ToLowerInvariant()] = relative;
            }
        }

        /// <summary>热重载 watcher（M8）检测到目录结构变化时调用，强制下次解析重新扫描。</summary>
        internal void InvalidateIndex()
        {
            indexed = false;
            lowercaseIndex = null;
        }
    }
}
