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
    /// <summary>
    /// 一个模组的资源句柄：挂载注册 + 全部取用入口。通过 <see cref="PolarisResAPI.For"/> 取得，
    /// 每个 <c>modId</c> 全进程单例。
    /// </summary>
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
        /// 约定挂载：调用方 DLL 所在目录下、与 DLL 同名的子文件夹（见
        /// <see cref="Polaris.Infra.PathsAPI.DefaultResRootOf"/>）——绝大多数模组只需要这一行，
        /// 或者干脆不调用这一行，靠 <see cref="Runtime.AutoBindScanner"/> 全自动完成
        /// （前提是这个模组只通过 <see cref="PolarisResourceAttribute"/> 静态字段用资源）。
        /// 用 <see cref="Assembly.GetCallingAssembly"/> 取得的是"调这个方法的那个程序集"，
        /// 所以这行必须由模组自己的代码直接调用，不能包一层再转发（否则取到的就是转发者
        /// 自己的目录，而不是模组的目录）。
        /// </summary>
        /// <remarks>
        /// <see cref="MethodImplOptions.NoInlining"/>：<c>GetCallingAssembly</c> 靠的是调用栈上
        /// 的调用帧；如果这个方法被内联进调用方，栈帧就没了，取到的"调用方程序集"会变成
        /// 再上一级的调用方，在 Release 编译或 Mono 的某些 JIT 策略下这不是假设性风险。
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ModResources MountDefault(int priority = 0)
        {
            return Mount(PolarisAPI.Paths.DefaultResRootOf(Assembly.GetCallingAssembly()), priority);
        }

        /// <summary>
        /// 挂载任意绝对路径。开发期指向源目录用这个，优先级给高一点（或干脆不给，
        /// 后注册的同优先级挂载本来就会先被探测到）即可覆盖发行目录。
        /// </summary>
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

        /// <summary>
        /// 读取 <c>.png</c>/<c>.jpg</c> 为裸 <see cref="Texture2D"/>。导入设置（filter/wrap/
        /// mipmap/readable/sRGB/anisoLevel/compress）由旁路 JSON 元数据决定——挂载根到文件
        /// 所在目录逐层的 <c>_import.json</c>，再叠加该文件自己的 <c>&lt;file&gt;.import.json</c>，
        /// 都没有则落到 <see cref="TextureImportSettings"/> 的内置默认值。见
        /// <see cref="ImportMetaResolver.ResolveTexture"/>。
        /// </summary>
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
        /// 读取图像并包成游戏能直接消费的 <see cref="XX.MImage"/>（材质/Shader 缓存，
        /// <c>getMtr(...)</c> 拿到的 <see cref="Material"/> 可以直接喂给 <c>MeshDrawer</c>）。
        /// <para>
        /// 内部复用 <see cref="Texture"/> 的缓存：持有一个内部 <c>Texture</c> 租约，
        /// 这个 <c>Image</c> 缓存条目释放时才跟着释放那个内部租约——两个 Kind 是各自独立的
        /// 缓存条目（<see cref="ResourceId"/> 把 <see cref="ResourceKind"/> 计入相等性判断），
        /// 但底层同一张 <see cref="Texture2D"/> 只会被磁盘读取/解码一次。
        /// </para>
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
                        // 纹理归底层 Texture 缓存条目所有（它自己的 Unloader 会在其引用计数
                        // 归零时 DestroyImmediate），MImage.Dispose() 不应该重复销毁它。
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
        /// 读取 PixelLiner 角色（<c>.pxls</c>/<c>.pxl</c>）。和 <see cref="Texture"/>/<see cref="Image"/>/
        /// <see cref="Bytes"/> 不同，PXLS 天生跨帧——游戏自己的 <c>PxlsLoader.loadCharacterASync</c>
        /// 内部靠协程解析，绕不开（旧计划"已核实的关键事实 #1"）。这里立即返回一个
        /// <see cref="PxlsCharacterHandle"/>，订阅它的 <c>Ready</c>/<c>Faulted</c> 事件获知结果——
        /// 不存在"加载失败时的占位 PXLS 角色"这种东西，失败语义天然是异步的。
        /// <para>
        /// 必须在 <see cref="Polaris.API.GameSessionRuntime.IsReady"/> 之后调用：PXLS 解析依赖 <c>MTRX.OMI</c>/
        /// <c>OMeshImages</c> 这两个只在 <c>MTRX.init1()</c> 之后才存在的静态字典，太早调用会直接
        /// 抛 <see cref="InvalidOperationException"/>（用法错误，不受"严格模式"设置影响）。
        /// </para>
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
                // 默认前缀必须带上资源自己的 path，不能只用 modId：同一个模组的多个 PXLS 角色
                // 只要有同名的 pose（idle、walk 很常见）就会在 XX.MTRX.OMeshImages 里算出一模一样的
                // 限定帧名，后加载的角色静默覆盖先加载的；先卸载的一方调用 PxlsRegistration.Unregister
                // 时还会把这个共享键置空，连带清掉仍然存活的后一个角色的帧。id.Path 已经是规范化
                // （小写、正斜杠）的挂载相对路径，天然是每个角色独一份的命名空间。
                string prefix = settings.FrameNamePrefix ?? (ModId + "/" + id.Path + "/");

                PxlCharacter character = PxlsLoader.loadCharacterASync(title, bytes, null, settings.PixelsPerUnit, settings.AutoFlipX);
                if (character == null)
                {
                    throw new ResourceLoadException(
                        id, $"PXLS load failed: title \"{title}\" already exists (a previous load of the same path may not have been released properly).");
                }

                // no_load_external_texture_on_first 必须为 true，否则解析期会尝试用
                // external_png_header 去 Resources.Load，抛异常被吞成 ERROR（旧计划"已核实的
                // 关键事实 #1"）。external_png_header 本身不用手动设置——no_load_external_texture_on_first
                // 为 true 时它从不会被真的拿去读文件，构造函数已经给了一个基于 title 的默认值，够用。
                character.no_load_external_texture_on_first = true;

                PxlsCharacterHandle handle = new PxlsCharacterHandle(id, title, settings.FrameNamePolicy, prefix);
                PxlsLoadOperation operation = new PxlsLoadOperation(
                    handle, character, absolutePath, mountRoot, title, settings.FrameNamePolicy, prefix);
                PxlsPump.Enqueue(operation);

                return (handle, (Action)operation.RequestDispose);
            });
        }

        /// <summary>
        /// 读取 <c>.wav</c>/<c>.ogg</c> 为 <see cref="AudioClip"/>。游戏自己的音频走 CRIWARE
        /// cue sheet，不认裸 wav/ogg 文件，所以这里没有游戏封装可复用，直接产出 Unity 原生
        /// <see cref="AudioClip"/>——播放（循环与否、音量……）完全交给调用方的
        /// <c>AudioSource</c>，PolarisRes 只负责解码。见 <see cref="Loaders.AudioLoader"/>。
        /// </summary>
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

        /// <summary>
        /// 解析 <c>.mp4</c> 的绝对路径，包成 <see cref="VideoHandle"/>。Unity 运行时没法从裸字节
        /// 直接构造可用的 <see cref="UnityEngine.Video.VideoClip"/>（那只能来自导入或 Bundle），
        /// 所以这里不读取文件内容，只解析路径——调用方自己建 <c>VideoPlayer</c>，把
        /// <c>url</c> 设成 <see cref="VideoHandle.AbsolutePath"/> 即可直接从磁盘播放。
        /// </summary>
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

        /// <summary>
        /// 同上，另外带出解析到的绝对路径 + 命中的挂载根——<see cref="Texture"/> 要拿这两个
        /// 去驱动 <see cref="ImportMetaResolver"/> 的目录链查找。
        /// </summary>
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

        /// <summary>
        /// 扫描调用方自己程序集里的全部类型，把标了 <see cref="PolarisResourceAttribute"/>
        /// 的 static 字段一次性填好。绝大多数模组在挂载完成后调这一个无参重载就够了。
        /// </summary>
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
                // PXLS 解析依赖 MTRX.OMI/OMeshImages，只在 GameSessionRuntime.IsReady 之后才存在
                // （见 Pxls(string, PxlsImportSettings) 的文档）。AutoBindScanner 在 BepInEx 的
                // Start() 阶段（Plugin.Start 的子系统初始化里）就跑，此时游戏通常还没到那一步，
                // 立即调用只会撞上 InvalidOperationException——外层 BindStaticFields(Type) 的
                // try/catch 把它当成"这个字段绑定失败"记一条日志就放弃了，字段永远停在 null，
                // 没有任何重试。改成包进 WhenReady：已经就绪时会同步立即执行，效果和以前一样；
                // 还没就绪则注册一次性回调，等游戏真正就绪那一帧再绑定，不再需要模组作者自己
                // 对 [PolarisResource] 字段操心时序。
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
