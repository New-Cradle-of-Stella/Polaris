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
    /// PXLS 复合加载的状态驱动器，一个实例对应一次 <c>ModResources.Pxls(...)</c> 调用。
    /// 只有等待 <c>PxlCharacter</c> 解析完成这一段跨帧；解析完成后剩余步骤（建纹理、绑定、注册帧名）都同步跑完。
    /// 由 <see cref="Runtime.PxlsPump"/> 每帧调用 <see cref="Tick"/>。
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
                Fail(new ResourceLoadException(handle.Id, $"{title} failed to parse: {character.error_str}"));
                return;
            }

            if (!character.isLoadCompleted())
            {
                return;
            }

            if (teardownRequested)
            {
                // 租约在解析完成前就被释放了；协程无法干净取消，只能让它跑完，此时还未绑定任何外部状态，直接释放 title 槽位即可。
                PxlsLoader.disposeCharacter(title, dispose_image: true);
                IsDone = true;
                return;
            }

            Finish();
        }

        /// <summary>由 <c>ResourceCache</c> 引用计数归零时调用。解析未完成时只打标记留给下次 <see cref="Tick"/>；已 Ready 则立刻按顺序清理，避免脏数据污染共享的游戏全局状态。</summary>
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
                                handle.Id, $"{title} is missing external texture #{i} (none of the three candidate file names were found; see PxlsNaming).");
                        }

                        byte[] bytes;
                        try
                        {
                            bytes = File.ReadAllBytes(path);
                        }
                        catch (Exception ex)
                        {
                            throw new ResourceLoadException(handle.Id, $"Failed to read external texture #{i} of {title}: {path}", ex);
                        }

                        // 复用现有 TextureLoader/ImportMetaResolver：外置贴图同样吃 _import.json。
                        ResourceId textureId = new ResourceId(handle.Id.ModId, ResourceKind.Texture, handle.Id.Path + ".texture" + i);
                        TextureImportSettings textureSettings = ImportMetaResolver.ResolveTexture(mountRoot, path);
                        ownedTextures[i] = TextureLoader.FromBytes(bytes, textureId, textureSettings);
                    }

                    // 必须用 ReplaceExternalPng，不能用 AddExternalPng：Add 是追加，会把占位槽翻倍，图集渲染全透明。
                    character.ReplaceExternalPng(ownedTextures, _do_not_destruct: true);
                    image = new XX.MImage(ownedTextures[0]) { dispose_texture = false };
                }
                else
                {
                    Texture embedded = FirstEmbeddedTexture(character);
                    if (embedded == null)
                    {
                        throw new ResourceLoadException(
                            handle.Id, $"{title} has neither external textures nor an embedded image; the PXLS file may be corrupt.");
                    }

                    image = new XX.MImage(embedded) { dispose_texture = false };
                }

                // assignMI 必须晚于 ReplaceExternalPng；帧名注册必须晚于 assignMI，否则会撞上 MTRX.getMI 的空纹理陷阱。
                XX.MTRX.assignMI(character, image);
                registeredFrameKeys = PxlsRegistration.Register(character, framePolicy, framePrefix);

                XX.MImage roundTrip = XX.MTRX.getMI(character, no_make_mi: true);
                if (!ReferenceEquals(roundTrip, image))
                {
                    Plugin.Logger.LogError(
                        $"[PolarisRes] assignMI check failed for {title}: getMI(no_make_mi:true) did not return the MImage that was just bound. " +
                        "There may be an ordering regression -- check the call order in PxlsLoadOperation.Finish.");
                }

                succeeded = true;
                IsDone = true;
                handle.MarkReady(character, image, n);
            }
            catch (Exception ex)
            {
                // Finish 执行到一半失败时可能已建了部分纹理/绑定，必须清理干净，避免残留或泄漏。
                CleanupPartialFinish();
                Fail(ex as ResourceLoadException ?? new ResourceLoadException(handle.Id, $"{title} failed during the finish stage: {ex.Message}", ex));
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
            // disposing:false 避免销毁消费者仍持有引用的缓存 Material；MImage 由我们自己紧接着释放。dispose_mti 恒为 false，因为它什么也不释放。
            XX.MTRX.releaseMI(character, disposing: false, dispose_mti: false);
            image.DisposeMaterial();
            image.Dispose();
            // dispose_image:true 是安全的：外置槽 do_not_destruct==true，PxlCharacter.Destroy 只清空槽位，纹理本体归我们自己销毁。
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
