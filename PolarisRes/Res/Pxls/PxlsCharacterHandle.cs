using System;
using PixelLiner;

namespace Polaris.Res.Pxls
{
    /// <summary>
    /// 模组代码唯一能拿到的 PXLS 句柄；因解析要跨帧完成，调用点没法用同步 try/catch，只能立刻拿到 handle 后订阅 <see cref="Ready"/>/<see cref="Faulted"/>。
    /// <see cref="Character"/>/<see cref="Image"/> 在 <see cref="IsReady"/> 变 true 前恒为 <c>null</c>，避免提前暴露引发空纹理陷阱。
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

        /// <summary>按 <see cref="QualifiedFrameName"/> 从全局 <c>XX.MTRX.getPF</c> 取当前注册的帧；<see cref="FrameNamePolicy.None"/> 下恒返回 <c>null</c>。</summary>
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
            Raise(Ready, nameof(Ready));
        }

        internal void MarkFaulted(ResourceLoadException error)
        {
            Error = error;
            IsFaulted = true;
            Raise(Faulted, nameof(Faulted));
        }

        /// <summary>一个模组的回调炸了不该连累其它在途 PXLS 的收尾，所以异常只记日志、不外传。</summary>
        private void Raise(Action<PxlsCharacterHandle> callback, string callbackName)
        {
            try
            {
                callback?.Invoke(this);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[PolarisRes] The {callbackName} callback of {Title} threw an exception: {ex}");
            }
        }
    }
}
