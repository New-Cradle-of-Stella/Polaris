namespace Polaris.Res
{
    /// <summary>资源种类，决定 <see cref="Mounts.ResourceKindExtensions.CandidateExtensions"/> 探测哪些扩展名及最终构造出的运行时对象类型。</summary>
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
