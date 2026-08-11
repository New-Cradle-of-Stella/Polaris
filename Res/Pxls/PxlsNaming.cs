using System.Collections.Generic;
using System.IO;

namespace Polaris.Res.Pxls
{
    /// <summary>title 计算 + 外置纹理文件名候选链。两者都是纯字符串/路径运算，不碰任何游戏状态，
    /// 拆出来单独测试/复用（<see cref="Loaders.PxlsLoadOperation"/> 是唯一调用方）。</summary>
    internal static class PxlsNaming
    {
        /// <summary>
        /// <c>PxlsLoader</c> 的 title 字典是进程级全局的，与原版共享同一命名空间。
        /// <c>"pr:"</c> 前缀 + modId 避免撞原版和跨模组撞车——前提是同一个 <c>modId</c> 只有一个
        /// <c>ModResources</c> 实例，这一点由 <see cref="PolarisResAPI.For"/> 的单例性质保证。
        /// </summary>
        internal static string BuildTitle(string modId, string normalizedPath) => "pr:" + modId + "/" + normalizedPath;

        /// <summary>
        /// 三级候选文件名链，首个命中为准：① <c>&lt;name&gt;.png</c>（i=0）/<c>.parts.png</c>（i=1）/
        /// <c>.&lt;i&gt;.png</c>（i≥2）——推荐的友好命名；② <c>&lt;name&gt;.pxls.texture_&lt;i&gt;.png</c>
        /// ——过渡命名；③ <c>&lt;name&gt;.pxls.bytes.texture_&lt;i&gt;.png</c>——与原版 AssetBundle
        /// 内命名完全一致的兼容别名，方便把解包出来的原始文件直接放进去用。
        /// </summary>
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

        /// <summary>
        /// 按 <see cref="ExternalTextureCandidates"/> 的顺序找第一个存在的文件；不止一个候选存在时
        /// 记一条歧义警告（用的是第一个命中的，但作者可能没意识到还有别的候选也存在）。
        /// </summary>
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
                    $"[PolarisRes] {title} 的外置纹理 #{index} 同时存在多个候选命名，使用了第一个命中的 \"{hit}\"，建议只保留一份。");
            }

            return hit;
        }
    }
}
