using Polaris.Res.Import;
using UnityEngine;

namespace Polaris.Res.Loaders
{
    /// <summary>
    /// 从 PNG/JPG 字节构造 <see cref="Texture2D"/>，导入设置由 <see cref="TextureImportSettings"/>
    /// 驱动（旁路 JSON 元数据解析后的结果，见 <see cref="ImportMetaResolver.ResolveTexture"/>）。
    /// <para>
    /// M1/M2 阶段是纯同步的一次性静态方法；M4 引入 <c>IResourceJob</c> 之后，文件 I/O
    /// 会转到后台线程，但 <see cref="Texture2D"/> 的构造/<c>LoadImage</c>/<c>Apply</c>
    /// 仍必须留在主线程——这里先不提前建那层还用不上的跨帧抽象，等 M4 真正需要时
    /// 再把这个静态方法包进 Job（届时文件可能改名为 TextureJob）。
    /// </para>
    /// <para>
    /// 构造方式对齐游戏自己的 <c>PixelLiner.PxlImage.createFromPngRawData</c>：内置默认是
    /// <c>ARGB32</c>、不建 mipmap、<c>FilterMode.Point</c>、先 <c>LoadImage</c>（默认
    /// <c>markNonReadable=false</c>，CPU 数据先保留）再单独 <c>Apply</c> 决定是否丢弃 CPU 拷贝。
    /// 唯一刻意的默认差异是 <c>wrapMode</c>：原版从未设置，落到 Unity 默认的 <c>Repeat</c>；
    /// 这里默认改成 <c>Clamp</c> 避免图集边缘渗色——PXLS 的 UV 从不越出 [0,1]，行为等价但更安全，
    /// 需要原版那种 <c>Repeat</c> 平铺效果时可以通过导入元数据显式覆盖回去。
    /// </para>
    /// <para>
    /// <see cref="TextureImportSettings.Format"/> 目前不生效：<c>Texture2D.LoadImage</c>
    /// 会按图像内容本身重新决定内部像素格式，构造时传入的 <c>TextureFormat</c> 只是初始占位，
    /// 不会强制转换成别的格式——这与 Unity Editor 里"导入设置能强制格式"的直觉不同。
    /// 如果以后要支持真正的格式转换，需要额外一次 GPU 读回 + 手动重建纹理，这里先不做。
    /// </para>
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
            catch (System.Exception ex)
            {
                Object.DestroyImmediate(texture);
                throw new ResourceLoadException(id, $"Failed to decode image: {id}", ex);
            }

            if (!ok)
            {
                Object.DestroyImmediate(texture);
                throw new ResourceLoadException(id, $"Not valid PNG/JPG data: {id}");
            }

            if (settings.Compress != TextureCompression.None)
            {
                // Compress 要求纹理仍可读；必须在下面 Apply(makeNoLongerReadable) 之前做。
                try
                {
                    texture.Compress(highQuality: settings.Compress == TextureCompression.HighQuality);
                }
                catch (System.Exception ex)
                {
                    // 压缩失败（比如尺寸不是 4 的倍数）不应该让整张纹理加载失败，跳过压缩即可。
                    Plugin.Logger.LogWarning($"[PolarisRes] {id} failed to compress; skipped: {ex.Message}");
                }
            }

            texture.Apply(updateMipmaps: settings.Mipmaps, makeNoLongerReadable: !settings.Readable);
            texture.name = id.Path;
            return texture;
        }
    }
}
