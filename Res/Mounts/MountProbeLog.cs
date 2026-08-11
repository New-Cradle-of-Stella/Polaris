using System.Collections.Generic;
using System.Text;

namespace Polaris.Res.Mounts
{
    /// <summary>
    /// 记录一次解析尝试探测过的每一个候选，找不到时用来生成人类可读的诊断信息。
    /// 这是本库里最有价值的一条诊断输出——找不到资源时列出全部尝试过的挂载点和扩展名，
    /// 而不是一句语焉不详的 "not found"。
    /// </summary>
    internal sealed class MountProbeLog
    {
        private sealed class MountAttempt
        {
            internal string RootPath;
            internal int Priority;
            internal readonly List<(string RelativePath, bool Exists)> Candidates = new List<(string, bool)>();
        }

        private readonly ResourceId id;
        private readonly List<MountAttempt> attempts = new List<MountAttempt>();
        private string caseMismatchHint;

        internal MountProbeLog(ResourceId id)
        {
            this.id = id;
        }

        internal void BeginMount(string rootPath, int priority)
        {
            attempts.Add(new MountAttempt { RootPath = rootPath, Priority = priority });
        }

        internal void RecordProbe(string relativePath, bool exists)
        {
            attempts[attempts.Count - 1].Candidates.Add((relativePath, exists));
        }

        /// <summary>命中大小写不一致的文件时记一句提示，附在最终诊断信息末尾。</summary>
        internal void RecordCaseMismatch(string expected, string actual, string mountRoot)
        {
            caseMismatchHint =
                $"Hint: directory \"{mountRoot}\" contains files differing only in case -- expected \"{expected}\", found \"{actual}\". Please make the casing consistent.";
        }

        internal string BuildMessage()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[PolarisRes] Resource not found: ").Append(id).AppendLine();

            if (attempts.Count == 0)
            {
                // 最常见的原因是调用顺序反了：在 Mount()/MountDefault() 之前就发起了取用。
                sb.AppendLine("  This mod has not registered any mount points yet -- was a fetch method called before Mount()/MountDefault()?");
                return sb.ToString();
            }

            sb.AppendLine("  Mount points tried (in priority order):");

            foreach (MountAttempt attempt in attempts)
            {
                sb.Append("    [").Append(attempt.Priority).Append("] ").Append(attempt.RootPath).AppendLine();
                foreach ((string relativePath, bool exists) in attempt.Candidates)
                {
                    sb.Append("          ").Append(relativePath).Append("   ")
                      .AppendLine(exists ? "exists but could not be read" : "does not exist");
                }
            }

            if (caseMismatchHint != null)
            {
                sb.AppendLine("  " + caseMismatchHint);
            }

            return sb.ToString();
        }
    }
}
