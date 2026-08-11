using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Polaris.Res.Import
{
    /// <summary>
    /// 旁路 JSON 导入元数据的继承解析：内置默认值 → 挂载根到文件所在目录逐层的
    /// <c>_import.json</c> → 该文件自己的 <c>&lt;file&gt;.import.json</c>，就近覆盖。
    /// <para>
    /// 两级缓存：<see cref="directoryChains"/> 按目录缓存"内置默认值 + 到这一层为止的全部
    /// <c>_import.json</c>"的合并结果（同一目录下 N 个文件共享同一份链，不用各自重新遍历
    /// 目录树）；<see cref="textureResults"/> 按文件绝对路径缓存最终反序列化结果。
    /// M8 热重载接入时，watcher 检测到 <c>_import.json</c>/<c>*.import.json</c> 变化后调用
    /// <see cref="Invalidate"/> 使两级缓存失效。TODO(M8)：watcher 落地后由它调用；
    /// 在那之前没有调用方，缓存只增不减（导入设置在一次游戏会话内不会变）。
    /// </para>
    /// </summary>
    internal static class ImportMetaResolver
    {
        private const string DirectoryDefaultFileName = "_import.json";

        private static readonly Dictionary<string, JObject> directoryChains =
            new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, TextureImportSettings> textureResults =
            new Dictionary<string, TextureImportSettings>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, PxlsImportSettings> pxlsResults =
            new Dictionary<string, PxlsImportSettings>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 解析某个纹理文件最终生效的导入设置。<paramref name="mountRoot"/> 必须是命中这个文件的
        /// 那个挂载的根目录（不是随便哪个挂载）——目录链只应该从这个根往下走，不能越界
        /// 走到挂载根以外的祖先目录去找 <c>_import.json</c>。
        /// </summary>
        internal static TextureImportSettings ResolveTexture(string mountRoot, string absoluteFilePath) =>
            ResolveSection(textureResults, "texture", mountRoot, absoluteFilePath);

        /// <summary>
        /// 解析某个 PXLS 文件最终生效的导入设置。<paramref name="over"/> 非空时整体替换 JSON 解析
        /// 结果（不做字段级合并——这个不对称是有意的简化，见调用方 <c>ModResources.Pxls</c> 的说明），
        /// 此时也不参与缓存，因为返回值就是调用方自己传入的对象。
        /// </summary>
        internal static PxlsImportSettings ResolvePxls(string mountRoot, string absoluteFilePath, PxlsImportSettings over)
        {
            if (over != null)
            {
                return over;
            }

            return ResolveSection(pxlsResults, "pxls", mountRoot, absoluteFilePath);
        }

        /// <summary>
        /// 各资源种类共用的解析流程：按文件绝对路径查 <paramref name="cache"/>，未命中则走
        /// "目录链 → 该文件自己的 <c>&lt;file&gt;.import.json</c>"合并，再反序列化出
        /// <paramref name="sectionName"/> 节。每加一个资源种类只需要多一个缓存字典 + 一个节名，
        /// 不用把这一整套流程再抄一遍。
        /// </summary>
        private static T ResolveSection<T>(
            Dictionary<string, T> cache, string sectionName, string mountRoot, string absoluteFilePath)
            where T : new()
        {
            if (cache.TryGetValue(absoluteFilePath, out T cached))
            {
                return cached;
            }

            JObject merged = BuildDirectoryChain(mountRoot, Path.GetDirectoryName(absoluteFilePath));

            JObject fileOverride = ImportMetaJson.TryLoad(absoluteFilePath + ".import.json");
            if (fileOverride != null)
            {
                merged = (JObject)merged.DeepClone();
                ImportMetaJson.MergeInto(merged, fileOverride);
            }

            T settings;
            try
            {
                settings = ImportMetaJson.DeserializeSection<T>(merged, sectionName);
            }
            catch (Exception ex)
            {
                // 拼错键 / 类型不匹配：报错但不让这一个文件的手滑拖垮整次加载，回退到内置默认值。
                Plugin.Logger.LogError(
                    $"[PolarisRes] 导入元数据里的 \"{sectionName}\" 节有误（用于 {absoluteFilePath}）：{ex.Message}，已回退到内置默认值。");
                settings = new T();
            }

            cache[absoluteFilePath] = settings;
            return settings;
        }

        /// <summary>
        /// 递归构造"挂载根 → <paramref name="directory"/>"这一路上逐层应用 <c>_import.json</c>
        /// 后的合并文档。递归顺序保证祖先目录先算（内置默认值打底），子目录的 <c>_import.json</c>
        /// 后应用，天然实现"就近覆盖"。
        /// </summary>
        private static JObject BuildDirectoryChain(string mountRoot, string directory)
        {
            string normalizedDirectory = NormalizeDirectory(directory);
            if (directoryChains.TryGetValue(normalizedDirectory, out JObject cached))
            {
                return cached;
            }

            JObject parentChain;
            string normalizedRoot = NormalizeDirectory(mountRoot);
            bool atOrAboveRoot = normalizedDirectory == null
                || normalizedDirectory.Length <= normalizedRoot.Length
                || string.Equals(normalizedDirectory, normalizedRoot, StringComparison.OrdinalIgnoreCase);

            if (atOrAboveRoot)
            {
                // 到达挂载根（或者传入的目录出乎意料地在根之外，防御性地当作根处理，
                // 不越界去遍历挂载范围以外的目录）：从内置默认值开始。
                parentChain = (JObject)ImportMetaJson.BuiltInDefaults.DeepClone();
                normalizedDirectory = normalizedRoot;
            }
            else
            {
                parentChain = BuildDirectoryChain(mountRoot, Path.GetDirectoryName(directory));
            }

            JObject ownDefault = ImportMetaJson.TryLoad(Path.Combine(directory ?? mountRoot, DirectoryDefaultFileName));
            JObject merged = (JObject)parentChain.DeepClone();
            if (ownDefault != null)
            {
                ImportMetaJson.MergeInto(merged, ownDefault);
            }

            directoryChains[normalizedDirectory] = merged;
            return merged;
        }

        private static string NormalizeDirectory(string directory) =>
            directory == null ? null : Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);

        /// <summary>清空全部缓存，供 M8 热重载在 <c>_import.json</c>/<c>*.import.json</c> 变化时调用。</summary>
        internal static void Invalidate()
        {
            directoryChains.Clear();
            textureResults.Clear();
            pxlsResults.Clear();
        }
    }
}
