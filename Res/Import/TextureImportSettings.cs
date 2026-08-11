using UnityEngine;

namespace Polaris.Res.Import
{
    /// <summary>
    /// 纹理导入设置，对应旁路 JSON 元数据（<c>_import.json</c>/<c>*.import.json</c>）里的
    /// <c>"texture"</c> 节。字段默认值就是内置默认值——<see cref="ImportMetaJson.BuiltInDefaults"/>
    /// 直接用 <c>new TextureImportSettings()</c> 序列化得到，不需要在两个地方各写一份。
    /// <para>
    /// 像素画正确，除 <see cref="WrapMode"/> 外与原版 <c>PxlImage.createFromPngRawData</c> 一致：
    /// 原版从不设置 <c>wrapMode</c>（落到 Unity 默认的 <c>Repeat</c>），这里默认改成
    /// <c>Clamp</c> 避免图集边缘渗色——PXLS 的 UV 从不越出 [0,1]，行为等价但更安全。
    /// </para>
    /// </summary>
    internal sealed class TextureImportSettings
    {
        public FilterMode FilterMode = FilterMode.Point;
        public TextureWrapMode WrapMode = TextureWrapMode.Clamp;
        public bool Mipmaps = false;
        public bool Readable = false;
        public bool SRGB = true;

        /// <summary>
        /// 目前不生效，仅用于让计划里约定的 <c>"format"</c> 键在 JSON 里合法（否则严格模式
        /// 下会被 <c>MissingMemberHandling.Error</c> 当成拼错的键报错）。<c>Texture2D.LoadImage</c>
        /// 会按图像内容本身重新决定内部像素格式，构造时传入的格式不会被强制生效——见
        /// <see cref="Loaders.TextureLoader"/> 里的说明。
        /// </summary>
        public TextureFormat Format = TextureFormat.ARGB32;

        public int AnisoLevel = 0;
        public TextureCompression Compress = TextureCompression.None;
    }

    /// <summary>
    /// <see cref="TextureImportSettings.Compress"/> 的取值。运行时构造的纹理来自
    /// <c>Texture2D.LoadImage</c>，不像 Unity Editor 导入那样有完整的 BC/ETC 压缩管线可选，
    /// 这里只暴露 <c>Texture2D.Compress(bool)</c> 实际支持的两档。
    /// </summary>
    internal enum TextureCompression
    {
        None,
        Normal,
        HighQuality,
    }
}
