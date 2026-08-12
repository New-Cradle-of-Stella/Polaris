namespace Polaris.Res
{
    /// <summary>
    /// 原始 <c>.mp4</c> 视频的轻量句柄，只保留绝对文件路径；播放交给调用方（用 <c>VideoPlayer.url</c> 指向 <see cref="AbsolutePath"/>）。
    /// <see cref="AbsolutePath"/> 为 <c>null</c> 表示"资源未找到"占位句柄（仅 <see cref="ResSettings.StrictMode"/> 关闭时出现）。
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
