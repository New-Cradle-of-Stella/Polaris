using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using PixelLiner;
using Polaris.Res.Core;
using Polaris.Res.Import;
using Polaris.Res.Loaders;
using Polaris.Res.Mounts;
using Polaris.Res.Pxls;
using Polaris.Res.Runtime;
using UnityEngine;

namespace Polaris.Res
{
    /// <summary>一个模组的资源句柄：挂载注册 + 全部取用入口。通过 <see cref="PolarisResAPI.For"/> 取得，每个 <c>modId</c> 全进程单例。</summary>
    public sealed class ModResources
    {
        private readonly MountTable mountTable = new MountTable();

        public string ModId { get; }

        /// <summary>"拿了不还"入口，见 <see cref="OwnerScope"/>。</summary>
        public OwnerScope Own { get; }

        internal ModResources(string modId)
        {
            ModId = modId;
            Own = new OwnerScope(this);
        }

        // ==================== 挂载 ====================

        /// <summary>
        /// 约定挂载：调用方 DLL 所在目录下、与 DLL 同名的子文件夹。必须由模组自己的代码直接调用，不能包一层再转发——否则 <see cref="Assembly.GetCallingAssembly"/> 会取到转发者的目录。
        /// </summary>
        /// <remarks><see cref="MethodImplOptions.NoInlining"/> 防止方法被内联导致 <c>GetCallingAssembly</c> 取错调用帧。</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ModResources MountDefault(int priority = 0)
        {
            return Mount(PolarisAPI.Paths.DefaultResRootOf(Assembly.GetCallingAssembly()), priority);
        }

        /// <summary>挂载任意绝对路径；开发期可指向源目录并给更高优先级来覆盖发行目录。</summary>
        public ModResources Mount(string absoluteRoot, int priority = 0)
        {
            if (string.IsNullOrEmpty(absoluteRoot))
            {
                throw new ArgumentException("absoluteRoot cannot be empty.", nameof(absoluteRoot));
            }

            mountTable.Add(absoluteRoot, priority);
            return this;
        }

        public IReadOnlyList<MountInfo> Mounts
        {
            get
            {
                List<MountInfo> result = new List<MountInfo>(mountTable.Mounts.Count);
                foreach (DirectoryMount mount in mountTable.Mounts)
                {
                    result.Add(new MountInfo(mount.RootPath, mount.Priority));
                }

                return result;
            }
        }

        public bool TryResolve(ResourceId id, out string absolutePath) =>
            mountTable.TryResolve(id, out absolutePath, out _);

        // ==================== 同步取用 ====================

        /// <summary>读取原始字节。<paramref name="path"/> 必须自带扩展名（不做扩展名探测）。</summary>
        public IResourceLease<byte[]> Bytes(string path)
        {
            ResourceId id = new ResourceId(ModId, ResourceKind.Bytes, path);
            return ResourceCache.AcquireSync<byte[]>(id, () => (LoadBytes(id), null));
        }

        /// <summary>读取 <c>.png</c>/<c>.jpg</c> 为裸 <see cref="Texture2D"/>；导入设置由旁路 JSON 元数据决定（逐层 <c>_import.json</c> 叠加同名 <c>.import.json</c>），见 <see cref="ImportMetaResolver.ResolveTexture"/>。</summary>
        public IResourceLease<Texture2D> Texture(string path)
        {
            ResourceId id = new ResourceId(ModId, ResourceKind.Texture, path);
            return ResourceCache.AcquireSync<Texture2D>(id, () =>
            {
                byte[] bytes = LoadBytes(id, out string absolutePath, out string mountRoot);
                TextureImportSettings settings = ImportMetaResolver.ResolveTexture(mountRoot, absolutePath);
                Texture2D texture = TextureLoader.FromBytes(bytes, id, settings);
                return (texture, (Action)(() => UnityEngine.Object.DestroyImmediate(texture)));
            });
        }

        /// <summary>
        /// 读取图像并包成游戏能直接消费的 <see cref="XX.MImage"/>（材质/Shader 缓存）。
        /// 内部复用 <see cref="Texture"/> 的缓存（持有内部 <c>Texture</c> 租约），底层同一张纹理只会被读取/解码一次。
        /// </summary>
        public IResourceLease<XX.MImage> Image(string path)
        {
            ResourceId id = new ResourceId(ModId, ResourceKind.Image, path);
            return ResourceCache.AcquireSync<XX.MImage>(id, () =>
            {
                IResourceLease<Texture2D> textureLease = Texture(path);
                XX.MImage image;
                try
                {
                    image = new XX.MImage(textureLease.Value)
                    {
                        // 纹理归底层 Texture 缓存条目所有，MImage.Dispose() 不应重复销毁它。
                        dispose_texture = false,
                    };
                }
                catch
                {
                    textureLease.Dispose();
                    throw;
                }

                void Unload()
                {
                    image.DisposeMaterial();
                    image.Dispose();
                    textureLease.Dispose();
                }

                return (image, (Action)Unload);
            });
        }

        /// <summary>
        /// 读取 PixelLiner 角色（<c>.pxls</c>/<c>.pxl</c>）。PXLS 天生跨帧（游戏的协程解析绕不开），立即返回 <see cref="PxlsCharacterHandle"/>，订阅其 <c>Ready</c>/<c>Faulted</c> 事件获知结果。
        /// 必须在 <see cref="Polaris.API.GameSessionRuntime.IsReady"/> 之后调用，否则抛 <see cref="InvalidOperationException"/>（不受严格模式影响）。
        /// </summary>
        public IResourceLease<PxlsCharacterHandle> Pxls(string path, PxlsImportSettings over = null)
        {
            ResourceId id = new ResourceId(ModId, ResourceKind.Pxls, path);
            return ResourceCache.AcquireSync<PxlsCharacterHandle>(id, () =>
            {
                if (!API.GameSessionRuntime.IsReady)
                {
                    throw new InvalidOperationException(
                        $"[PolarisRes] {id} loaded too early: PXLS must be loaded after the game is ready. " +
                        "Wrap the call in a API.GameSessionRuntime.WhenReady(...) callback.");
                }

                byte[] bytes = LoadBytes(id, out string absolutePath, out string mountRoot);
                PxlsImportSettings settings = ImportMetaResolver.ResolvePxls(mountRoot, absolutePath, over);
                string title = PxlsNaming.BuildTitle(ModId, id.Path);
                // 默认前缀必须带上资源自己的 path，不能只用 modId，否则同名 pose（idle/walk）会在不同角色间撞名。
                string prefix = settings.FrameNamePrefix ?? (ModId + "/" + id.Path + "/");

                PxlCharacter character = PxlsLoader.loadCharacterASync(title, bytes, null, settings.PixelsPerUnit, settings.AutoFlipX);
                if (character == null)
                {
                    throw new ResourceLoadException(
                        id, $"PXLS load failed: title \"{title}\" already exists (a previous load of the same path may not have been released properly).");
                }

                // 必须为 true，否则解析期会尝试用 external_png_header 去 Resources.Load，抛异常被吞成 ERROR。
                character.no_load_external_texture_on_first = true;

                PxlsCharacterHandle handle = new PxlsCharacterHandle(id, title, settings.FrameNamePolicy, prefix);
                PxlsLoadOperation operation = new PxlsLoadOperation(
                    handle, character, absolutePath, mountRoot, title, settings.FrameNamePolicy, prefix);
                PxlsPump.Enqueue(operation);

                return (handle, (Action)operation.RequestDispose);
            });
        }

        /// <summary>读取 <c>.wav</c>/<c>.ogg</c> 为 Unity 原生 <see cref="AudioClip"/>；播放交给调用方的 <c>AudioSource</c>，见 <see cref="Loaders.AudioLoader"/>。</summary>
        public IResourceLease<AudioClip> Audio(string path)
        {
            ResourceId id = new ResourceId(ModId, ResourceKind.Audio, path);
            return ResourceCache.AcquireSync<AudioClip>(id, () =>
            {
                byte[] bytes = LoadBytes(id, out string absolutePath, out _);
                AudioClip clip = AudioLoader.FromBytes(bytes, absolutePath, id);
                return (clip, (Action)(() => UnityEngine.Object.DestroyImmediate(clip)));
            });
        }

        /// <summary>解析 <c>.mp4</c> 的绝对路径，包成 <see cref="VideoHandle"/>；只解析路径不读取内容，调用方自建 <c>VideoPlayer</c> 从磁盘播放。</summary>
        public IResourceLease<VideoHandle> Video(string path)
        {
            ResourceId id = new ResourceId(ModId, ResourceKind.Video, path);
            return ResourceCache.AcquireSync<VideoHandle>(id, () =>
            {
                if (!mountTable.TryResolve(id, out string absolutePath, out _, out MountProbeLog probeLog))
                {
                    throw new ResourceNotFoundException(id, probeLog.BuildMessage());
                }

                return (new VideoHandle(absolutePath), (Action)null);
            });
        }

        private byte[] LoadBytes(ResourceId id) => LoadBytes(id, out _, out _);

        /// <summary>同上，另外带出解析到的绝对路径与命中的挂载根，供 <see cref="ImportMetaResolver"/> 目录链查找用。</summary>
        private byte[] LoadBytes(ResourceId id, out string absolutePath, out string mountRoot)
        {
            if (!mountTable.TryResolve(id, out absolutePath, out mountRoot, out MountProbeLog probeLog))
            {
                throw new ResourceNotFoundException(id, probeLog.BuildMessage());
            }

            try
            {
                return File.ReadAllBytes(absolutePath);
            }
            catch (Exception ex)
            {
                throw new ResourceLoadException(id, $"Failed to read file: {absolutePath}", ex);
            }
        }

        // ==================== [PolarisResource] 静态字段绑定 ====================

        /// <summary>扫描调用方自己程序集里的全部类型，把标了 <see cref="PolarisResourceAttribute"/> 的 static 字段一次性填好。</summary>
        /// <returns>本次成功绑定的字段数。</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int BindStaticFields()
        {
            return BindStaticFields(Assembly.GetCallingAssembly());
        }

        /// <summary>扫描指定程序集里的全部类型。</summary>
        /// <returns>本次成功绑定的字段数。</returns>
        public int BindStaticFields(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            int bound = 0;
            foreach (Type type in PolarisAPI.Types.Of(assembly))
            {
                bound += BindStaticFields(type);
            }

            return bound;
        }

        /// <summary>只扫描单个类型——如果想缩小范围，或者要绑定别的程序集里的类型，用这个重载。</summary>
        /// <returns>本次成功绑定的字段数。</returns>
        public int BindStaticFields(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            int bound = 0;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var attr = (PolarisResourceAttribute)Attribute.GetCustomAttribute(field, typeof(PolarisResourceAttribute));
                if (attr == null)
                {
                    continue;
                }

                if (field.IsInitOnly || field.IsLiteral)
                {
                    Plugin.Logger.LogWarning(
                        $"[PolarisRes] {type.FullName}.{field.Name} is readonly/const and cannot be back-filled; skipped.");
                    continue;
                }

                try
                {
                    BindField(field, attr.Path);
                    bound++;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError(
                        $"[PolarisRes] Failed to bind {type.FullName}.{field.Name} (\"{attr.Path}\"): {ex}");
                }
            }

            return bound;
        }

        /// <summary>按字段类型分派到对应的 <see cref="Own"/> 方法。字段类型不受支持时抛异常
        /// （由 <see cref="BindStaticFields(Type)"/> 捕获并记日志，不会中断其余字段的绑定）。</summary>
        private void BindField(FieldInfo field, string path)
        {
            Type fieldType = field.FieldType;

            if (fieldType == typeof(byte[]))
            {
                field.SetValue(null, Own.Bytes(path));
                return;
            }

            if (fieldType == typeof(Texture2D))
            {
                field.SetValue(null, Own.Texture(path));
                return;
            }

            if (fieldType == typeof(XX.MImage))
            {
                field.SetValue(null, Own.Image(path));
                return;
            }

            if (fieldType == typeof(PxlsCharacterHandle))
            {
                // PXLS 解析依赖只在游戏就绪后才存在的字典；AutoBindScanner 通常在游戏就绪前跑，
                // 所以包进 WhenReady：已就绪立即执行，否则注册一次性回调等就绪后再绑定。
                API.GameSessionRuntime.WhenReady(() =>
                {
                    try
                    {
                        field.SetValue(null, Own.Pxls(path));
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError(
                            $"[PolarisRes] Deferred binding of {field.DeclaringType?.FullName}.{field.Name} (\"{path}\") failed: {ex}");
                    }
                });
                return;
            }

            if (fieldType == typeof(AudioClip))
            {
                field.SetValue(null, Own.Audio(path));
                return;
            }

            if (fieldType == typeof(VideoHandle))
            {
                field.SetValue(null, Own.Video(path));
                return;
            }

            throw new NotSupportedException(
                $"Field type {fieldType.Name} is not supported for auto-binding yet. Currently supported: byte[] / Texture2D / XX.MImage / " +
                "PxlsCharacterHandle / AudioClip / VideoHandle.");
        }
    }
}
