namespace Polaris.Res
{
    /// <summary>
    /// 资源种类。决定了 <see cref="Mounts.ResourceKindExtensions.CandidateExtensions"/>
    /// 探测哪些扩展名，以及最终构造出的运行时对象类型。
    /// <para>
    /// 目前固定为这六种（jpg/png 图片、PXLS、wav/ogg 音频、mp4 视频）；以后要支持新格式，
    /// 只是加一个枚举值 + <see cref="Mounts.ResourceKindExtensions.CandidateExtensions"/> 里加一条
    /// 候选扩展名 + <see cref="ModResources"/>/<see cref="OwnerScope"/> 里各加一个对应分支——
    /// 不需要额外的插件式注册机制，现在这套按字段类型 if/switch 分派足够简单，
    /// 先不为了"可扩展"提前抽象。
    /// </para>
    /// </summary>
    public enum ResourceKind
    {
        /// <summary>原始字节，路径必须自带扩展名（不做任何探测）。</summary>
        Bytes,

        /// <summary>裸 <c>UnityEngine.Texture2D</c>。</summary>
        Texture,

        /// <summary>包了材质缓存的 <c>XX.MImage</c>。</summary>
        Image,

        /// <summary>PixelLiner 角色（<c>.pxls</c>/<c>.pxl</c>）。</summary>
        Pxls,

        /// <summary>原始音频（<c>.wav</c>/<c>.ogg</c>/<c>.mp3</c>）。</summary>
        Audio,

        /// <summary>原始视频（<c>.mp4</c>）。</summary>
        Video,
    }
}
