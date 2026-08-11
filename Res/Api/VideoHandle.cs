namespace Polaris.Res
{
    /// <summary>
    /// 原始 <c>.mp4</c> 视频的轻量句柄。游戏和 Unity 都没有"从裸字节直接构造可用 <c>VideoClip</c>"
    /// 这条路（<c>VideoClip</c> 只能来自导入或 AssetBundle），所以这里不尝试伪造一个 <c>VideoClip</c>，
    /// 只保留解析出的绝对文件路径——播放交给调用方：自己建一个
    /// <c>UnityEngine.Video.VideoPlayer</c>，把 <c>url</c> 设成 <see cref="AbsolutePath"/>
    /// 即可直接从磁盘播放，不需要 <c>VideoClip</c> 资产。PolarisRes 不管播放器的创建/生命周期。
    /// <para>
    /// <see cref="AbsolutePath"/> 为 <c>null</c> 表示这是"资源未找到"时的占位句柄（见
    /// <see cref="OwnerScope.Video"/>，仅在 <see cref="ResSettings.StrictMode"/> 关闭时出现）。
    /// </para>
    /// </summary>
    public sealed class VideoHandle
    {
        internal VideoHandle(string absolutePath)
        {
            AbsolutePath = absolutePath;
        }

        public string AbsolutePath { get; }
    }
}
