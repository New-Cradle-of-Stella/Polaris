using System;

namespace Polaris.Res.Core
{
    /// <summary>
    /// <see cref="ResourceCache"/> 主表里的一条记录。字段先按后续里程碑的完整设计给出
    /// （即使 M1 只用得上 <see cref="State"/>/<see cref="Value"/>/<see cref="RefCount"/>/
    /// <see cref="Error"/>），因为这是所有加载路径共用的同一个类型，晚加字段意味着
    /// 到时候要回头改已经在用的构造点；先留好位置成本很低。
    /// </summary>
    internal sealed class ResourceCacheEntry
    {
        internal ResourceId Id;
        internal ResourceState State;

        /// <summary>Ready 状态下的真正值：byte[]/Texture2D/MImage/PxlsCharacterHandle/ModAudioClip。</summary>
        internal object Value;

        internal int RefCount;

#pragma warning disable 649 // 要到 M8 热重载才会真正写入；提前留位置，字段先声明进共用的 Entry 类型里。
        /// <summary>每次热重载 +1；M8 之前恒为 0。</summary>
        internal int Version;
#pragma warning restore 649

#pragma warning disable 649 // 要到 M4 异步加载才会真正写入。
        internal Exception Error;
#pragma warning restore 649

        /// <summary>卸载时调用的清理动作（比如销毁 Texture2D）；M2 起由 Texture/Image 加载路径写入。</summary>
        internal Action Unloader;
    }
}
