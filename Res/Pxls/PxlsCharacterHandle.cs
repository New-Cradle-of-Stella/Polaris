using System;
using PixelLiner;

namespace Polaris.Res.Pxls
{
    /// <summary>
    /// 模组代码唯一能拿到的 PXLS 句柄。和 <c>Texture</c>/<c>Image</c>/<c>Bytes</c> 不同，
    /// 找不到文件之外的失败（PXLS 损坏、title 冲突）要等 <c>PxlsLoader</c> 自己的协程跑完
    /// 几帧之后才能发现，任何调用点的 <c>try/catch</c> 都够不着——所以这里没有"拿到能用的
    /// 对象或者异常"这种同步语义，天然是"立刻拿到一个 handle，订阅 <see cref="Ready"/>/
    /// <see cref="Faulted"/>"。
    /// <para>
    /// <see cref="Character"/>/<see cref="Image"/> 在 <see cref="IsReady"/> 变 true 之前恒为
    /// <c>null</c>——这是刻意的：解析完成前把 <c>PxlCharacter</c> 暴露出去，模组代码就有可能
    /// 经 <c>getPF</c>/<c>getMI(PxlFrame)</c>/<c>getMI(PxlImage)</c> 间接触发
    /// <c>XX.MTRX.getMI</c> 的空纹理陷阱（旧计划"已核实的关键事实 #12"）。
    /// </para>
    /// </summary>
    public sealed class PxlsCharacterHandle
    {
        private readonly FrameNamePolicy framePolicy;
        private readonly string framePrefix;

        public ResourceId Id { get; }

        /// <summary><c>PxlsLoader</c> 全局 title 字典里的键，见 <see cref="PxlsNaming.BuildTitle"/>。</summary>
        public string Title { get; }

        public bool IsReady { get; private set; }
        public bool IsFaulted { get; private set; }
        public ResourceLoadException Error { get; private set; }
        public int ExternalTextureCount { get; private set; }

        /// <summary>未就绪为 <c>null</c>。</summary>
        public PxlCharacter Character { get; private set; }

        /// <summary>未就绪为 <c>null</c>。</summary>
        public XX.MImage Image { get; private set; }

        public event Action<PxlsCharacterHandle> Ready;
        public event Action<PxlsCharacterHandle> Faulted;

        internal PxlsCharacterHandle(ResourceId id, string title, FrameNamePolicy framePolicy, string framePrefix)
        {
            Id = id;
            Title = title;
            this.framePolicy = framePolicy;
            this.framePrefix = framePrefix ?? "";
        }

        /// <summary>永远走当前 <see cref="Character"/>——不要跨帧缓存返回值。</summary>
        public PxlPose GetPose(string name) => Character?.getPoseByName(name);

        /// <summary>
        /// 按 <see cref="QualifiedFrameName"/> 从全局 <c>XX.MTRX.getPF</c> 里取当前注册的帧。
        /// <see cref="FrameNamePolicy.None"/> 策略下什么都没注册，恒返回 <c>null</c>——
        /// 用了 <c>None</c> 就意味着调用方打算自己走 <see cref="GetPose"/> 拿帧，不依赖这个。
        /// </summary>
        public PxlFrame GetFrame(string frameName) => XX.MTRX.getPF(QualifiedFrameName(frameName));

        /// <summary>裸帧名 → 实际注册进 <c>OMeshImages</c> 的键。</summary>
        public string QualifiedFrameName(string frameName) =>
            framePolicy == FrameNamePolicy.Prefixed ? framePrefix + frameName : frameName;

        internal void MarkReady(PxlCharacter character, XX.MImage image, int externalTextureCount)
        {
            Character = character;
            Image = image;
            ExternalTextureCount = externalTextureCount;
            IsReady = true;

            try
            {
                Ready?.Invoke(this);
            }
            catch (Exception ex)
            {
                // 一个模组的 Ready 回调炸了，不该连累同一帧里其它在途 PXLS 的收尾。
                Plugin.Logger.LogError($"[PolarisRes] {Title} 的 Ready 回调抛出异常：{ex}");
            }
        }

        internal void MarkFaulted(ResourceLoadException error)
        {
            Error = error;
            IsFaulted = true;

            try
            {
                Faulted?.Invoke(this);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[PolarisRes] {Title} 的 Faulted 回调抛出异常：{ex}");
            }
        }
    }
}
