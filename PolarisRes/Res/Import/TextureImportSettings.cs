using UnityEngine;

namespace Polaris.Res.Import
{
    /// <summary>
    /// 纹理导入设置，对应旁路 JSON 元数据里的 <c>"texture"</c> 节；字段默认值即内置默认值。
    /// <see cref="WrapMode"/> 故意改为 <c>Clamp</c>（原版用 <c>Repeat</c>）以避免图集边缘渗色。
    /// </summary>
    internal sealed class TextureImportSettings
    {
        public FilterMode FilterMode = FilterMode.Point;
        public TextureWrapMode WrapMode = TextureWrapMode.Clamp;
        public bool Mipmaps = false;
        public bool Readable = false;
        public bool SRGB = true;

        /// <summary>目前不生效——<c>Texture2D.LoadImage</c> 按图像内容自行决定像素格式；此字段仅用于让 JSON 里的 <c>"format"</c> 键在严格模式下合法。</summary>
        public TextureFormat Format = TextureFormat.ARGB32;

        public int AnisoLevel = 0;
        public TextureCompression Compress = TextureCompression.None;
    }

    /// <summary><see cref="TextureImportSettings.Compress"/> 的取值，对应 <c>Texture2D.Compress(bool)</c> 支持的两档。</summary>
    internal enum TextureCompression
    {
        None,
        Normal,
        HighQuality,
    }
}
