using System;

namespace Polaris.Res.Core
{
    /// <summary>
    /// <see cref="ResourceCache"/> 主表里的一条记录，是所有加载路径共用的同一个类型。
    /// </summary>
    internal sealed class ResourceCacheEntry
    {
        internal ResourceId Id;
        internal ResourceState State;

        /// <summary>Ready 状态下的真正值：byte[]/Texture2D/MImage/PxlsCharacterHandle/ModAudioClip。</summary>
        internal object Value;

        internal int RefCount;

#pragma warning disable 649 // 由异步加载/热重载路径写入，当前构建里恒为默认值。
        /// <summary>每次热重载 +1。</summary>
        internal int Version;

        internal Exception Error;
#pragma warning restore 649

        /// <summary>卸载时调用的清理动作（比如销毁 Texture2D）。</summary>
        internal Action Unloader;
    }
}
