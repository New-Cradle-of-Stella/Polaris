using Polaris.Res.Pxls;

namespace Polaris.Res.Import
{
    /// <summary>PXLS 导入设置，对应旁路 JSON 元数据里的 <c>"pxls"</c> 节。和
    /// <see cref="TextureImportSettings"/> 同一形状：字段默认值就是内置默认值。
    /// <para>
    /// 和只在内部消费的 <see cref="TextureImportSettings"/> 不同，这个类型是 <c>public</c>——
    /// <c>ModResources.Pxls(string, PxlsImportSettings)</c> 把它当代码级覆盖参数直接暴露给
    /// 模组作者，公开方法的参数类型不能比方法本身更不可见。
    /// </para>
    /// </summary>
    public sealed class PxlsImportSettings
    {
        public float PixelsPerUnit = 64f;
        public bool AutoFlipX = true;
        public FrameNamePolicy FrameNamePolicy = FrameNamePolicy.Prefixed;

        /// <summary><c>null</c> 表示用 <c>"&lt;modId&gt;/&lt;path&gt;/"</c> 这个默认前缀（在解析出
        /// modId 和资源 path 之后才能算出来，所以这里不能把默认值直接写成字符串常量）。
        /// path 段是必要的：只有 modId 不足以区分同一模组下的多个 PXLS 角色，idle/walk 之类
        /// 的常见 pose 名会互相撞车。</summary>
        public string FrameNamePrefix = null;
    }
}
