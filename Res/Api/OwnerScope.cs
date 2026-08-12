using System;
using System.Collections.Generic;
using Polaris.Res.Import;
using Polaris.Res.Pxls;
using UnityEngine;

namespace Polaris.Res
{
    /// <summary>
    /// "拿了不还"入口：按路径去重、永不需要手动 Dispose，生命周期与所属 <see cref="ModResources"/> 绑定，统一通过 <see cref="ReleaseAll"/> 释放。
    /// 找不到/加载失败时的行为由 <see cref="ResSettings.StrictMode"/> 控制：开启抛异常，关闭则记录错误日志并返回占位对象（不影响参数错误之类的用法错误照常抛出）。
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

        /// <summary>找不到/解码失败时的占位是一段极短的静音 <see cref="AudioClip"/>。</summary>
        public AudioClip Audio(string path) => Get(
            new ResourceId(owner.ModId, ResourceKind.Audio, path),
            () => owner.Audio(path),
            CreatePlaceholderAudio);

        /// <summary>找不到时的占位是 <see cref="VideoHandle.AbsolutePath"/> 为 <c>null</c> 的 <see cref="VideoHandle"/>。</summary>
        public VideoHandle Video(string path) => Get(
            new ResourceId(owner.ModId, ResourceKind.Video, path),
            () => owner.Video(path),
            () => (new VideoHandle(null), (IDisposable)null));

        /// <summary>找不到文件/读取失败时返回一个立即 <c>Faulted</c> 的占位句柄（PXLS 没有占位角色，与 <see cref="Texture"/>/<see cref="Image"/> 的占位纹理不同）。</summary>
        public PxlsCharacterHandle Pxls(string path, PxlsImportSettings over = null)
        {
            ResourceId id = new ResourceId(owner.ModId, ResourceKind.Pxls, path);
            return Get(
                id,
                () => owner.Pxls(path, over),
                () =>
                {
                    PxlsCharacterHandle handle = new PxlsCharacterHandle(id, "<placeholder>", FrameNamePolicy.None, "");
                    handle.MarkFaulted(new ResourceLoadException(id, "PXLS load failed; a placeholder (Faulted) handle is used instead."));
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
                Plugin.Logger.LogError($"[PolarisRes] {id} failed to load; using a placeholder object instead: {ex.Message}");
                (T value, IDisposable cleanup) = placeholder();
                held[id] = (value, cleanup);
                return value;
            }
        }

        /// <summary>释放这个作用域持有的全部资源；目前只能手动调用。</summary>
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
