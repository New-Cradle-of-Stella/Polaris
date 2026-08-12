using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Polaris.Res.Import
{
    /// <summary>
    /// 唯一接触 Newtonsoft.Json 的文件。用 <see cref="JObject.Merge(JToken, JsonMergeSettings)"/> + <see cref="MergeNullValueHandling.Merge"/> 实现三态语义：
    /// JSON 键缺席=不覆盖，显式 <c>null</c>=重置为内置默认，有值=覆盖。
    /// 引用游戏自带的 Newtonsoft.Json（非 NuGet），避免程序集身份冲突。
    /// </summary>
    internal static class ImportMetaJson
    {
        /// <summary>Schema 里已命名但当前构建还没有对应 DTO 的节；出现在 JSON 里合法，不应报"未知节名"警告。</summary>
        private static readonly HashSet<string> ReservedSectionNames =
            new HashSet<string>(StringComparer.Ordinal) { "texture", "pxls", "audio", "video" };

        private static readonly JsonSerializer StrictSerializer = new JsonSerializer
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        private static readonly JsonMergeSettings MergeSettings = new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge,
        };

        /// <summary>内置默认值文档，每节直接由对应 Settings 类型的默认实例序列化得到（默认值只在 Settings 类里维护一份）。</summary>
        internal static readonly JObject BuiltInDefaults = new JObject
        {
            ["$schema"] = "polarisres/import/1",
            ["texture"] = JObject.FromObject(new TextureImportSettings()),
            ["pxls"] = JObject.FromObject(new PxlsImportSettings()),
        };

        /// <summary>
        /// 读取、解析并校验一个 <c>.import.json</c> 文件；不存在返回 <c>null</c>，语法错误或拼错键都记日志并返回 <c>null</c>（整份覆盖作废，不中断其它加载）。
        /// 校验必须紧跟 <see cref="JObject.Parse(string)"/> 之后做——合并后再校验会丢失行列号诊断信息。
        /// </summary>
        internal static JObject TryLoad(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                return null;
            }

            JObject document;
            try
            {
                // JObject.Parse 内部用 JsonTextReader，默认保留行列信息。
                document = JObject.Parse(File.ReadAllText(jsonFilePath));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(
                    $"[PolarisRes] Failed to parse import metadata ({jsonFilePath}): {ex.Message}. Ignoring this file (treated as no overrides).");
                return null;
            }

            return ValidateKnownSections(document, jsonFilePath) ? document : null;
        }

        private static bool ValidateKnownSections(JObject document, string sourcePath)
        {
            bool allValid = true;

            foreach (JProperty property in document.Properties())
            {
                if (string.Equals(property.Name, "$schema", StringComparison.Ordinal))
                {
                    continue;
                }

                Type dtoType = ResolveSectionType(property.Name);
                if (dtoType == null)
                {
                    if (!ReservedSectionNames.Contains(property.Name))
                    {
                        Plugin.Logger.LogWarning(
                            $"[PolarisRes] Section \"{property.Name}\" in {sourcePath} is not a known kind and is not on the reserved list " +
                            "(texture/pxls/audio/video); ignored -- check for a misspelled section name.");
                    }
                    // 保留名单内但当前构建还没实现 DTO 的节：安静跳过，不校验也不警告。
                    continue;
                }

                if (property.Value.Type == JTokenType.Null)
                {
                    // 整节显式 null：合法，代表"这一层要把这一节整体重置成上一层的默认值"。
                    continue;
                }

                if (!(property.Value is JObject sectionObject))
                {
                    Plugin.Logger.LogError(
                        $"[PolarisRes] Section \"{property.Name}\" of {sourcePath} must be a JSON object; ignoring this override.");
                    allValid = false;
                    continue;
                }

                try
                {
                    // 仅用克隆探路验证字段合法性，真正生效的仍是原始 sectionObject。
                    StripNullProperties(sectionObject).ToObject(dtoType, StrictSerializer);
                }
                catch (JsonSerializationException ex)
                {
                    Plugin.Logger.LogError(
                        $"[PolarisRes] Section \"{property.Name}\" of {sourcePath} is malformed: {ex.Message}" +
                        $" (line {ex.LineNumber}, column {ex.LinePosition}); ignoring this override.");
                    allValid = false;
                }
            }

            return allValid;
        }

        private static Type ResolveSectionType(string sectionName)
        {
            switch (sectionName)
            {
                case "texture":
                    return typeof(TextureImportSettings);
                case "pxls":
                    return typeof(PxlsImportSettings);
                default:
                    return null;
            }
        }

        /// <summary>把 <paramref name="overlay"/> 合并进 <paramref name="target"/>（原地修改），调用方负责需要时先 <c>DeepClone</c>。</summary>
        internal static void MergeInto(JObject target, JObject overlay) => target.Merge(overlay, MergeSettings);

        /// <summary>从合并后的文档取出 <paramref name="sectionName"/> 节并反序列化成 <typeparamref name="T"/>；节缺席或显式 <c>null</c> 都返回默认实例。</summary>
        internal static T DeserializeSection<T>(JObject document, string sectionName) where T : new()
        {
            JToken section = document[sectionName];
            if (section == null || section.Type == JTokenType.Null)
            {
                return new T();
            }

            return StripNullProperties((JObject)section).ToObject<T>(StrictSerializer);
        }

        /// <summary>去掉值为 JSON <c>null</c> 的顶层键（返回新对象），避免值类型字段反序列化 <c>null</c> 时抛异常；效果等价于把该字段重置为默认值。</summary>
        private static JObject StripNullProperties(JObject section)
        {
            JObject clone = (JObject)section.DeepClone();
            List<string> nullKeys = null;
            foreach (JProperty property in clone.Properties())
            {
                if (property.Value.Type == JTokenType.Null)
                {
                    (nullKeys ??= new List<string>()).Add(property.Name);
                }
            }

            if (nullKeys != null)
            {
                foreach (string key in nullKeys)
                {
                    clone.Remove(key);
                }
            }

            return clone;
        }
    }
}
