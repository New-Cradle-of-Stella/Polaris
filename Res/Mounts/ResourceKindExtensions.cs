using System;
using System.Collections.Generic;

namespace Polaris.Res.Mounts
{
    /// <summary>按 <see cref="ResourceKind"/> 探测候选扩展名，探测顺序即优先级。</summary>
    internal static class ResourceKindExtensions
    {
        private static readonly string[] TextureExtensions = { ".png", ".jpg", ".jpeg" };
        private static readonly string[] PxlsExtensions = { ".pxls", ".pxl" };
        // mp3 暂不在候选列表里：目前没有 mp3 解码器（见 Loaders/AudioLoader.cs）。
        private static readonly string[] AudioExtensions = { ".ogg", ".wav" };
        private static readonly string[] VideoExtensions = { ".mp4" };
        private static readonly string[] NoExtensions = Array.Empty<string>();

        /// <summary>空数组表示不做扩展名探测（<see cref="ResourceKind.Bytes"/> 专用，路径必须自带扩展名）。</summary>
        internal static IReadOnlyList<string> CandidateExtensions(this ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Texture:
                case ResourceKind.Image:
                    return TextureExtensions;
                case ResourceKind.Pxls:
                    return PxlsExtensions;
                case ResourceKind.Audio:
                    return AudioExtensions;
                case ResourceKind.Video:
                    return VideoExtensions;
                default:
                    return NoExtensions;
            }
        }
    }
}
