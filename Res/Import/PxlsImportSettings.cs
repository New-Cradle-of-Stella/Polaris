using Polaris.Res.Pxls;

namespace Polaris.Res.Import
{
    /// <summary>
    /// PXLS 导入设置，对应旁路 JSON 元数据里的 <c>"pxls"</c> 节；字段默认值即内置默认值。
    /// 类型公开，因为 <c>ModResources.Pxls(string, PxlsImportSettings)</c> 把它作为代码级覆盖参数暴露给模组作者。
    /// </summary>
    public sealed class PxlsImportSettings
    {
        public float PixelsPerUnit = 64f;
        public bool AutoFlipX = true;
        public FrameNamePolicy FrameNamePolicy = FrameNamePolicy.Prefixed;

        /// <summary><c>null</c> 表示用默认前缀 <c>"&lt;modId&gt;/&lt;path&gt;/"</c>（需解析出资源 path 后才能算出，故不能写成常量）。</summary>
        public string FrameNamePrefix = null;
    }
}
