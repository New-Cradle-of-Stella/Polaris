using System.Collections.Generic;
using System.Text;

namespace Polaris.Res.Mounts
{
    /// <summary>记录一次解析尝试探测过的每个候选，找不到时用来生成列出全部尝试过的挂载点/扩展名的诊断信息。</summary>
    internal sealed class MountProbeLog
    {
        private sealed class MountAttempt
        {
            internal string RootPath;
            internal int Priority;
            internal readonly List<string> MissedCandidates = new List<string>();
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

        /// <summary>记下一个没能命中的候选相对路径，归到最近一次 <see cref="BeginMount"/> 的挂载点下。</summary>
        internal void RecordMiss(string relativePath)
        {
            attempts[attempts.Count - 1].MissedCandidates.Add(relativePath);
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
                // 常见原因：在 Mount()/MountDefault() 之前就发起了取用。
                sb.AppendLine("  This mod has not registered any mount points yet -- was a fetch method called before Mount()/MountDefault()?");
                return sb.ToString();
            }

            sb.AppendLine("  Mount points tried (in priority order):");

            foreach (MountAttempt attempt in attempts)
            {
                sb.Append("    [").Append(attempt.Priority).Append("] ").Append(attempt.RootPath).AppendLine();
                foreach (string relativePath in attempt.MissedCandidates)
                {
                    sb.Append("          ").Append(relativePath).AppendLine("   does not exist");
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
