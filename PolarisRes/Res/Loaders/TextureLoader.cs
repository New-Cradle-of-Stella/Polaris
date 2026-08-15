using System;
using Polaris.Res.Import;
using UnityEngine;

namespace Polaris.Res.Loaders
{
    /// <summary>
    /// 从 PNG/JPG 字节构造 <see cref="Texture2D"/>，导入设置由 <see cref="TextureImportSettings"/> 驱动（见 <see cref="ImportMetaResolver.ResolveTexture"/>）。
    /// 构造方式对齐游戏自己的 <c>PixelLiner.PxlImage.createFromPngRawData</c>，唯一刻意差异是 <c>wrapMode</c> 默认改为 <c>Clamp</c>（原版为 <c>Repeat</c>）以避免图集边缘渗色。
    /// <see cref="TextureImportSettings.Format"/> 不生效——<c>Texture2D.LoadImage</c> 会按图像内容自行决定像素格式。
    /// </summary>
    internal static class TextureLoader
    {
        internal static Texture2D FromBytes(byte[] bytes, ResourceId id, TextureImportSettings settings)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, mipChain: settings.Mipmaps, linear: !settings.SRGB)
            {
                filterMode = settings.FilterMode,
                wrapMode = settings.WrapMode,
                anisoLevel = settings.AnisoLevel,
            };

            bool ok;
            try
            {
                ok = texture.LoadImage(bytes, markNonReadable: false);
            }
            catch (Exception ex)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new ResourceLoadException(id, $"Failed to decode image: {id}", ex);
            }

            if (!ok)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new ResourceLoadException(id, $"Not valid PNG/JPG data: {id}");
            }

            if (settings.Compress != TextureCompression.None)
            {
                // Compress 要求纹理仍可读；必须在下面 Apply(makeNoLongerReadable) 之前做。
                try
                {
                    texture.Compress(highQuality: settings.Compress == TextureCompression.HighQuality);
                }
                catch (Exception ex)
                {
                    // 压缩失败（如尺寸非 4 的倍数）不应让整张纹理加载失败，跳过即可。
                    Plugin.Logger.LogWarning($"[PolarisRes] {id} failed to compress; skipped: {ex.Message}");
                }
            }

            texture.Apply(updateMipmaps: settings.Mipmaps, makeNoLongerReadable: !settings.Readable);
            texture.name = id.Path;
            return texture;
        }
    }
}
