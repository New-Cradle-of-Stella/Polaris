using System.Collections.Generic;
using System.IO;

namespace Polaris.Res.Pxls
{
    /// <summary>title 计算 + 外置纹理文件名候选链。两者都是纯字符串/路径运算，不碰任何游戏状态，
    /// 拆出来单独测试/复用（<see cref="Loaders.PxlsLoadOperation"/> 是唯一调用方）。</summary>
    internal static class PxlsNaming
    {
        /// <summary><c>PxlsLoader</c> 的 title 字典是进程级全局的；<c>"pr:"</c> 前缀 + modId 避免撞原版和跨模组撞车。</summary>
        internal static string BuildTitle(string modId, string normalizedPath) => "pr:" + modId + "/" + normalizedPath;

        /// <summary>三级候选文件名链，首个命中为准：①友好命名（<c>name.png</c>/<c>.parts.png</c>/<c>.&lt;i&gt;.png</c>）②过渡命名③与原版 AssetBundle 命名一致的兼容别名。</summary>
        internal static IReadOnlyList<string> ExternalTextureCandidates(string pxlsAbsolutePath, int index)
        {
            string directory = Path.GetDirectoryName(pxlsAbsolutePath) ?? "";
            string baseName = Path.GetFileNameWithoutExtension(pxlsAbsolutePath);
            string pxlsFileName = Path.GetFileName(pxlsAbsolutePath);

            string friendly = index switch
            {
                0 => baseName + ".png",
                1 => baseName + ".parts.png",
                _ => baseName + "." + index + ".png",
            };

            return new[]
            {
                Path.Combine(directory, friendly),
                Path.Combine(directory, pxlsFileName + ".texture_" + index + ".png"),
                Path.Combine(directory, pxlsFileName + ".bytes.texture_" + index + ".png"),
            };
        }

        /// <summary>按 <see cref="ExternalTextureCandidates"/> 顺序找第一个存在的文件；不止一个候选存在时记一条歧义警告。</summary>
        internal static string ResolveExternalTexturePath(string pxlsAbsolutePath, int index, string title)
        {
            IReadOnlyList<string> candidates = ExternalTextureCandidates(pxlsAbsolutePath, index);
            string hit = null;
            int existingCount = 0;

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    existingCount++;
                    if (hit == null)
                    {
                        hit = candidate;
                    }
                }
            }

            if (existingCount > 1)
            {
                Plugin.Logger.LogWarning(
                    $"[PolarisRes] External texture #{index} of {title} has several candidate names at once; using the first match \"{hit}\". Keeping only one copy is recommended.");
            }

            return hit;
        }
    }
}
