using System;
using System.Collections.Generic;
using System.IO;
using PixelLiner;
using Polaris.Res.Import;
using Polaris.Res.Pxls;
using UnityEngine;

namespace Polaris.Res.Loaders
{
    /// <summary>
    /// PXLS 复合加载的状态驱动器：一个实例对应一次 <c>ModResources.Pxls(...)</c> 调用。
    /// <para>
    /// 只有一段真正跨帧——等待 <c>PxlCharacter.isLoadCompleted()</c>/<c>errorOccured()</c> 翻转，
    /// 因为 <c>PxlsLoader.loadCharacterASync</c> 内部自己起了协程，这是绕不开的（旧计划"已核实的
    /// 关键事实 #1"）。其余步骤（读外置纹理字节、建 <c>Texture2D</c>、绑定、注册帧名）都在
    /// 解析完成的那一帧内同步跑完——0～2 张小 PNG 不是性能问题，不值得为此再建一层跨帧调度。
    /// </para>
    /// <para>
    /// 由 <see cref="Runtime.PxlsPump"/> 每帧调用一次 <see cref="Tick"/>；构造它的调用方
    /// （<c>ModResources.Pxls</c>）负责把它注册进 <see cref="Runtime.PxlsPump"/>。
    /// </para>
    /// </summary>
    internal sealed class PxlsLoadOperation
    {
        private readonly PxlsCharacterHandle handle;
        private readonly PxlCharacter character;
        private readonly string absolutePxlsPath;
        private readonly string mountRoot;
        private readonly string title;
        private readonly FrameNamePolicy framePolicy;
        private readonly string framePrefix;

        private List<string> registeredFrameKeys;
        private Texture2D[] ownedTextures;
        private XX.MImage image;
        private bool teardownRequested;
        private bool succeeded;

        internal bool IsDone { get; private set; }

        internal PxlsLoadOperation(
            PxlsCharacterHandle handle,
            PxlCharacter character,
            string absolutePxlsPath,
            string mountRoot,
            string title,
            FrameNamePolicy framePolicy,
            string framePrefix)
        {
            this.handle = handle;
            this.character = character;
            this.absolutePxlsPath = absolutePxlsPath;
            this.mountRoot = mountRoot;
            this.title = title;
            this.framePolicy = framePolicy;
            this.framePrefix = framePrefix;
        }

        internal void Tick()
        {
            if (IsDone)
            {
                return;
            }

            if (character.errorOccured())
            {
                Fail(new ResourceLoadException(handle.Id, $"{title} 解析失败：{character.error_str}"));
                return;
            }

            if (!character.isLoadCompleted())
            {
                return;
            }

            if (teardownRequested)
            {
                // 租约在解析完成之前就被释放了：PxlsLoader 的协程没法干净取消（旧计划风险 #3），
                // 只能让它跑完；跑完这一刻我们还没绑定任何外部状态（Finish 还没执行），
                // 直接释放游戏自己的 title 槽位即可，不需要走完整的 Disposing 顺序。
                PxlsLoader.disposeCharacter(title, dispose_image: true);
                IsDone = true;
                return;
            }

            Finish();
        }

        /// <summary>
        /// 由 <c>ResourceCache</c> 的卸载动作调用（引用计数归零时）。解析还没完成时只是打个标记，
        /// 留给下一次 <see cref="Tick"/> 处理；已经 <see cref="PxlsCharacterHandle.Ready"/> 过的话
        /// 立刻按 Disposing 顺序清理——这一步即使不做 M6 生命周期硬化也必须做对，否则会把脏数据
        /// 留在 <c>MTRX.OMI</c>/<c>OMeshImages</c>/<c>PxlsLoader</c> 的全局 title 字典里，
        /// 污染共享的游戏状态。
        /// </summary>
        internal void RequestDispose()
        {
            if (!IsDone)
            {
                teardownRequested = true;
                return;
            }

            if (succeeded)
            {
                TeardownReady();
            }
            // Faulted 的情况：Fail() 里已经 disposeCharacter 过了，这里不需要再做什么。
        }

        private void Finish()
        {
            try
            {
                PxlsTexture[] slots = character.getExternalTextureArray();
                int n = slots?.Length ?? 0;

                if (n > 0)
                {
                    ownedTextures = new Texture2D[n];
                    for (int i = 0; i < n; i++)
                    {
                        string path = PxlsNaming.ResolveExternalTexturePath(absolutePxlsPath, i, title);
                        if (path == null)
                        {
                            throw new ResourceLoadException(
                                handle.Id, $"{title} 缺少外置纹理 #{i}（三级候选文件名都没找到，见 PxlsNaming）。");
                        }

                        byte[] bytes;
                        try
                        {
                            bytes = File.ReadAllBytes(path);
                        }
                        catch (Exception ex)
                        {
                            throw new ResourceLoadException(handle.Id, $"读取 {title} 的外置纹理 #{i} 失败：{path}", ex);
                        }

                        // 复用现有 TextureLoader/ImportMetaResolver：这些外置贴图依然吃
                        // _import.json/*.import.json，不用另写一套纹理构造代码。
                        ResourceId textureId = new ResourceId(handle.Id.ModId, ResourceKind.Texture, handle.Id.Path + ".texture" + i);
                        TextureImportSettings textureSettings = ImportMetaResolver.ResolveTexture(mountRoot, path);
                        ownedTextures[i] = TextureLoader.FromBytes(bytes, textureId, textureSettings);
                    }

                    // 必须是 ReplaceExternalPng，绝不是 AddExternalPng——理由见旧计划"已核实的
                    // 关键事实 #3"：Add 是追加，在解析完成后调用会把占位槽翻倍，图集渲染全透明。
                    character.ReplaceExternalPng(ownedTextures, _do_not_destruct: true);
                    image = new XX.MImage(ownedTextures[0]) { dispose_texture = false };
                }
                else
                {
                    Texture embedded = FirstEmbeddedTexture(character);
                    if (embedded == null)
                    {
                        throw new ResourceLoadException(
                            handle.Id, $"{title} 既没有外置纹理也没有内嵌图像，PXLS 文件可能已损坏。");
                    }

                    image = new XX.MImage(embedded) { dispose_texture = false };
                }

                // assignMI 必须晚于 ReplaceExternalPng；帧名注册必须晚于 assignMI——顺序错了会
                // 撞上 MTRX.getMI 的空纹理陷阱（旧计划"已核实的关键事实 #12"）。
                XX.MTRX.assignMI(character, image);
                registeredFrameKeys = PxlsRegistration.Register(character, framePolicy, framePrefix);

                XX.MImage roundTrip = XX.MTRX.getMI(character, no_make_mi: true);
                if (!ReferenceEquals(roundTrip, image))
                {
                    Plugin.Logger.LogError(
                        $"[PolarisRes] {title} 的 assignMI 校验失败：getMI(no_make_mi:true) 返回的不是刚绑定的 MImage，" +
                        "可能有顺序回归，请检查 PxlsLoadOperation.Finish 的调用顺序。");
                }

                succeeded = true;
                IsDone = true;
                handle.MarkReady(character, image, n);
            }
            catch (Exception ex)
            {
                // Finish 执行到一半就失败，可能已经建了几张纹理/绑了部分 MTRX 状态，
                // 必须清理干净，不能就地放着——否则残留半初始化的 OMI/OMeshImages 条目或纹理泄漏。
                CleanupPartialFinish();
                Fail(ex as ResourceLoadException ?? new ResourceLoadException(handle.Id, $"{title} 完成阶段失败：{ex.Message}", ex));
            }
        }

        private static Texture FirstEmbeddedTexture(PxlCharacter character)
        {
            Dictionary<PxlImage.PxlImageId, PxlImage> images = character.getImageObject();
            if (images == null)
            {
                return null;
            }

            foreach (KeyValuePair<PxlImage.PxlImageId, PxlImage> entry in images)
            {
                return entry.Value.get_I();
            }

            return null;
        }

        private void Fail(ResourceLoadException error)
        {
            PxlsLoader.disposeCharacter(title, dispose_image: true);
            IsDone = true;
            handle.MarkFaulted(error);
        }

        private void CleanupPartialFinish()
        {
            if (registeredFrameKeys != null)
            {
                PxlsRegistration.Unregister(registeredFrameKeys);
                registeredFrameKeys = null;
            }

            if (image != null)
            {
                XX.MTRX.releaseMI(character, disposing: false, dispose_mti: false);
                image.DisposeMaterial();
                image.Dispose();
                image = null;
            }

            DestroyOwnedTextures();
        }

        private void TeardownReady()
        {
            PxlsRegistration.Unregister(registeredFrameKeys);
            // disposing:false 保证不会把消费者仍持有引用的缓存 Material 销毁掉——MImage 由我们
            // 自己紧接着按节奏释放；dispose_mti 恒为 false，反编译确认它只查一下 MTI 就丢弃，
            // 什么也不释放，没必要为此拖入 MTI（旧计划"已核实的关键事实 #12"）。
            XX.MTRX.releaseMI(character, disposing: false, dispose_mti: false);
            image.DisposeMaterial();
            image.Dispose();
            // dispose_image:true 是安全的：我们的外置槽 do_not_destruct==true，PxlCharacter.Destroy
            // 只会把槽位置空，纹理本体（归我们所有）安然无恙，接下来自己销毁。
            PxlsLoader.disposeCharacter(title, dispose_image: true);
            DestroyOwnedTextures();
        }

        private void DestroyOwnedTextures()
        {
            if (ownedTextures == null)
            {
                return;
            }

            foreach (Texture2D texture in ownedTextures)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            ownedTextures = null;
        }
    }
}
