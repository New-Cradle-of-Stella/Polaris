using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Polaris.Res.Import
{
    /// <summary>
    /// 旁路 JSON 导入元数据的继承解析：内置默认值 → 挂载根到文件目录逐层的 <c>_import.json</c> → 文件自己的 <c>.import.json</c>，就近覆盖。
    /// 两级缓存（按目录、按文件路径）只增不减，需要失效时调用 <see cref="Invalidate"/>。
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

        /// <summary>解析某个纹理文件最终生效的导入设置；<paramref name="mountRoot"/> 必须是命中该文件的挂载根目录，目录链不会越界到根以外查找。</summary>
        internal static TextureImportSettings ResolveTexture(string mountRoot, string absoluteFilePath) =>
            ResolveSection(textureResults, "texture", mountRoot, absoluteFilePath);

        /// <summary>解析某个 PXLS 文件最终生效的导入设置；<paramref name="over"/> 非空时整体替换 JSON 结果（不做字段级合并），且不参与缓存。</summary>
        internal static PxlsImportSettings ResolvePxls(string mountRoot, string absoluteFilePath, PxlsImportSettings over)
        {
            if (over != null)
            {
                return over;
            }

            return ResolveSection(pxlsResults, "pxls", mountRoot, absoluteFilePath);
        }

        /// <summary>各资源种类共用的解析流程：查缓存，未命中则合并目录链与文件级覆盖，再反序列化出 <paramref name="sectionName"/> 节。</summary>
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
                // 拼错键/类型不匹配：报错但回退到内置默认值，不拖垮整次加载。
                Plugin.Logger.LogError(
                    $"[PolarisRes] Section \"{sectionName}\" in the import metadata is malformed (used for {absoluteFilePath}): {ex.Message}. Falling back to built-in defaults.");
                settings = new T();
            }

            cache[absoluteFilePath] = settings;
            return settings;
        }

        /// <summary>递归构造挂载根到 <paramref name="directory"/> 逐层应用 <c>_import.json</c> 的合并文档，祖先目录先算，实现就近覆盖。</summary>
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
                // 到达挂载根（或目录意外在根之外，防御性当作根处理）：从内置默认值开始。
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

        /// <summary>清空全部缓存，供 <c>_import.json</c>/<c>*.import.json</c> 变化时调用。</summary>
        internal static void Invalidate()
        {
            directoryChains.Clear();
            textureResults.Clear();
            pxlsResults.Clear();
        }
    }
}
