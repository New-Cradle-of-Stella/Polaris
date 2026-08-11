using System;
using System.Collections.Generic;
using Polaris.Res.Import;
using Polaris.Res.Pxls;
using UnityEngine;

namespace Polaris.Res
{
    /// <summary>
    /// "拿了不还"入口：一次性获取、按路径去重（同一个 path 重复取返回同一个值，
    /// 内部只保留一份租约，引用计数不会因为反复调用而叠加）、永不需要手动 Dispose。
    /// 生命周期与所属 <see cref="ModResources"/> 绑定，统一通过 <see cref="ReleaseAll"/> 释放。
    /// <para>
    /// 找不到/加载失败时的行为由 <see cref="ResSettings.StrictMode"/>（"严格模式"设置）控制：
    /// 开启则原样抛出异常；默认关闭时记录一条错误日志，返回一个占位对象（4×4 品红纹理，
    /// 或空字节数组）——这样模组里一个缺失的美术资源不会直接把整个初始化流程带崩。
    /// 这个兜底只覆盖 <see cref="ResourceNotFoundException"/>/<see cref="ResourceLoadException"/>
    /// 这两类"资源本身有问题"的异常；参数错误之类的调用方用法错误永远直接抛出，
    /// 不会被严格模式的开关悄悄吞掉。
    /// </para>
    /// </summary>
    public sealed class OwnerScope
    {
        private readonly ModResources owner;
        private readonly Dictionary<ResourceId, (object Value, IDisposable Cleanup)> held =
            new Dictionary<ResourceId, (object, IDisposable)>();

        internal OwnerScope(ModResources owner)
        {
            this.owner = owner;
        }

        public byte[] Bytes(string path) => Get(
            new ResourceId(owner.ModId, ResourceKind.Bytes, path),
            () => owner.Bytes(path),
            () => (Array.Empty<byte>(), (IDisposable)null));

        public Texture2D Texture(string path) => Get(
            new ResourceId(owner.ModId, ResourceKind.Texture, path),
            () => owner.Texture(path),
            CreatePlaceholderTexture);

        public XX.MImage Image(string path) => Get(
            new ResourceId(owner.ModId, ResourceKind.Image, path),
            () => owner.Image(path),
            CreatePlaceholderImage);

        /// <summary>找不到/解码失败时的占位是一段极短的静音 <see cref="AudioClip"/>——播放它不会
        /// 报错也不会有声音，和 <see cref="Texture"/> 用品红占位纹理的思路一致。</summary>
        public AudioClip Audio(string path) => Get(
            new ResourceId(owner.ModId, ResourceKind.Audio, path),
            () => owner.Audio(path),
            CreatePlaceholderAudio);

        /// <summary>找不到时的占位是 <see cref="VideoHandle.AbsolutePath"/> 为 <c>null</c> 的
        /// <see cref="VideoHandle"/>；调用方按这个字段判断"是不是占位"。</summary>
        public VideoHandle Video(string path) => Get(
            new ResourceId(owner.ModId, ResourceKind.Video, path),
            () => owner.Video(path),
            () => (new VideoHandle(null), (IDisposable)null));

        /// <summary>
        /// 找不到文件/读取失败时返回一个立即 <c>Faulted</c> 的占位句柄（订阅 <c>Faulted</c> 就能
        /// 感知到，不需要专门判断"是不是占位对象"）——PXLS 没有"占位角色"这种东西，这是和
        /// <see cref="Texture"/>/<see cref="Image"/> 的占位纹理/图像不同的地方。解析期才发生的
        /// 失败（PXLS 损坏、title 冲突）不受这里影响，那是异步的，走 <c>Faulted</c> 事件。
        /// </summary>
        public PxlsCharacterHandle Pxls(string path, PxlsImportSettings over = null)
        {
            ResourceId id = new ResourceId(owner.ModId, ResourceKind.Pxls, path);
            return Get(
                id,
                () => owner.Pxls(path, over),
                () =>
                {
                    PxlsCharacterHandle handle = new PxlsCharacterHandle(id, "<placeholder>", FrameNamePolicy.None, "");
                    handle.MarkFaulted(new ResourceLoadException(id, "PXLS 加载失败，已使用占位（Faulted）句柄代替。"));
                    return (handle, (IDisposable)null);
                });
        }

        private T Get<T>(ResourceId id, Func<IResourceLease<T>> acquire, Func<(T Value, IDisposable Cleanup)> placeholder)
        {
            if (held.TryGetValue(id, out (object Value, IDisposable Cleanup) existing))
            {
                return (T)existing.Value;
            }

            try
            {
                IResourceLease<T> lease = acquire();
                T value = lease.Value;
                held[id] = (value, lease);
                return value;
            }
            catch (Exception ex) when (!ResSettings.StrictMode
                && (ex is ResourceNotFoundException || ex is ResourceLoadException))
            {
                Plugin.Logger.LogError($"[PolarisRes] {id} 加载失败，使用占位对象代替：{ex.Message}");
                (T value, IDisposable cleanup) = placeholder();
                held[id] = (value, cleanup);
                return value;
            }
        }

        /// <summary>释放这个作用域持有的全部资源。目前只能手动调用；随模组生命周期
        /// （<c>OnApplicationQuit</c>、<c>ModResources.BindLifetime</c>）自动释放是 M6 的事。</summary>
        public void ReleaseAll()
        {
            foreach ((object _, IDisposable cleanup) in held.Values)
            {
                cleanup?.Dispose();
            }

            held.Clear();
        }

        private static (Texture2D Value, IDisposable Cleanup) CreatePlaceholderTexture()
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.ARGB32, mipChain: false, linear: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PolarisRes_Placeholder",
            };

            Color32 magenta = new Color32(255, 0, 255, 255);
            Color32[] pixels = new Color32[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = magenta;
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            return (texture, new DisposeAction(() => UnityEngine.Object.DestroyImmediate(texture)));
        }

        private static (XX.MImage Value, IDisposable Cleanup) CreatePlaceholderImage()
        {
            (Texture2D texture, IDisposable textureCleanup) = CreatePlaceholderTexture();
            XX.MImage image = new XX.MImage(texture) { dispose_texture = false };

            void Unload()
            {
                image.DisposeMaterial();
                image.Dispose();
                textureCleanup.Dispose();
            }

            return (image, new DisposeAction(Unload));
        }

        private static (AudioClip Value, IDisposable Cleanup) CreatePlaceholderAudio()
        {
            AudioClip clip = AudioClip.Create("PolarisRes_Placeholder", 1, 1, 44100, stream: false);
            clip.SetData(new float[1], 0);
            return (clip, new DisposeAction(() => UnityEngine.Object.DestroyImmediate(clip)));
        }

        private sealed class DisposeAction : IDisposable
        {
            private readonly Action action;

            internal DisposeAction(Action action)
            {
                this.action = action;
            }

            public void Dispose() => action?.Invoke();
        }
    }
}
