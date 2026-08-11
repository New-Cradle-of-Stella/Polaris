using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Polaris.Res.Import
{
    /// <summary>
    /// 唯一接触 Newtonsoft.Json 的文件。规则 4（"字段仅在 JSON 键物理存在时才覆盖；显式
    /// <c>null</c> = 重置为内置默认"）需要真正的三态（缺席/null/有值），<c>Nullable&lt;T&gt;</c>
    /// DTO 表达不了这个三态；<see cref="JObject.Merge(JToken, JsonMergeSettings)"/> 配合
    /// <see cref="MergeNullValueHandling.Merge"/> 恰好原生实现这个模型：JSON 里缺席的键
    /// 不出现在合并结果里，物理写了 <c>null</c> 的键会把目标里对应的键也覆盖成 <c>null</c>。
    /// <para>
    /// 引用游戏自带的 Newtonsoft.Json 13.0.2（<c>&lt;Private&gt;false&lt;/Private&gt;</c>），
    /// 不用 NuGet 包引入，避免程序集身份冲突——见 <c>PolarisRes.csproj</c> 里的说明。
    /// </para>
    /// </summary>
    internal static class ImportMetaJson
    {
        /// <summary>
        /// Schema 里已经命名、但当前构建还没有对应 DTO 的节（PXLS/音频/视频要等各自的
        /// 里程碑落地）。这些节出现在 JSON 里是完全合法的——不应该被当成"未知/拼错的节名"
        /// 报警告；只是暂时没有东西去消费它们，见 <see cref="ResolveSectionType"/>。
        /// </summary>
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

        /// <summary>
        /// 内置默认值文档：每个已实现的资源种类一节，直接由对应 Settings 类型的默认构造
        /// 实例序列化得到——默认值只在 Settings 类里写一份，这里不重复维护第二份。
        /// </summary>
        internal static readonly JObject BuiltInDefaults = new JObject
        {
            ["$schema"] = "polarisres/import/1",
            ["texture"] = JObject.FromObject(new TextureImportSettings()),
            ["pxls"] = JObject.FromObject(new PxlsImportSettings()),
        };

        /// <summary>
        /// 读取、解析并校验一个 <c>.import.json</c> 文件。不存在返回 <c>null</c>；JSON 语法
        /// 错误、或某一节内出现该节 DTO 没有的字段（拼错键），都记录一条带文件路径 + 行列号
        /// 的错误日志并返回 <c>null</c>（整份覆盖作废，视为"这份覆盖不存在"）——不会让
        /// 一个手滑的错误中断同目录下其它资源的加载，但也绝不静默吞掉错误。
        /// <para>
        /// 校验必须在这里、紧跟着 <see cref="JObject.Parse(string)"/> 之后做，而不是留到
        /// 后面合并完再做：<see cref="JObject.Merge(JToken, JsonMergeSettings)"/> 对"目标里
        /// 原本不存在的键"（典型的拼错键就是这种）不会保留来源 token 的行列信息（已用独立
        /// 测试确认），合并完再报错只能拿到 line 0 / col 0，毫无诊断价值。
        /// </para>
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
                // JObject.Parse 内部用 JsonTextReader，默认保留行列信息（IJsonLineInfo）。
                document = JObject.Parse(File.ReadAllText(jsonFilePath));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(
                    $"[PolarisRes] 解析导入元数据失败（{jsonFilePath}）：{ex.Message}，已忽略此文件（视为无覆盖）。");
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
                            $"[PolarisRes] {sourcePath} 里的节 \"{property.Name}\" 不是已知种类，也不在保留名单" +
                            "（texture/pxls/audio/video）里，已忽略——检查是否拼错节名。");
                    }
                    // 保留名单内但当前构建还没实现 DTO 的节（pxls/audio/video）：安静跳过，
                    // 不校验也不警告，等对应里程碑落地后这里会加上真正的 DTO。
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
                        $"[PolarisRes] {sourcePath} 的 \"{property.Name}\" 节必须是一个 JSON 对象，已忽略这份覆盖。");
                    allValid = false;
                    continue;
                }

                try
                {
                    // 只是拿它探路验证字段合法性，探路用的克隆不会被留用——真正生效的还是
                    // 原始 sectionObject（连同其 null 标记）参与后续合并。
                    StripNullProperties(sectionObject).ToObject(dtoType, StrictSerializer);
                }
                catch (JsonSerializationException ex)
                {
                    Plugin.Logger.LogError(
                        $"[PolarisRes] {sourcePath} 的 \"{property.Name}\" 节有误：{ex.Message}" +
                        $"（第 {ex.LineNumber} 行，第 {ex.LinePosition} 列），已忽略这份覆盖。");
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
                    // audio/video 的 DTO 会在各自里程碑（M5/M6）落地后加进这里。
                    return null;
            }
        }

        /// <summary>
        /// 把 <paramref name="overlay"/> 合并进 <paramref name="target"/>（原地修改
        /// <paramref name="target"/>）。调用方负责在需要保留原值时先 <c>DeepClone</c>。
        /// </summary>
        internal static void MergeInto(JObject target, JObject overlay) => target.Merge(overlay, MergeSettings);

        /// <summary>
        /// 从合并后的文档里取出 <paramref name="sectionName"/> 节并反序列化成
        /// <typeparamref name="T"/>。节缺席、或整节显式为 <c>null</c>，都返回全新的默认实例
        /// （字段初始化器本身就是内置默认值）。
        /// <para>
        /// 各层已经在 <see cref="TryLoad"/> 里单独校验过，理论上这里不会再遇到拼错的键；
        /// 仍然用 <c>try/catch</c> 兜底（防御性的，属于"不该发生但发生了"的那一类），
        /// 兜底路径不追求 line/col（原因见 <see cref="TryLoad"/> 的注释），只报节名。
        /// </para>
        /// </summary>
        internal static T DeserializeSection<T>(JObject document, string sectionName) where T : new()
        {
            JToken section = document[sectionName];
            if (section == null || section.Type == JTokenType.Null)
            {
                return new T();
            }

            return StripNullProperties((JObject)section).ToObject<T>(StrictSerializer);
        }

        /// <summary>
        /// 去掉值为 JSON <c>null</c> 的顶层键，返回一份新对象（不修改 <paramref name="section"/>
        /// 本身）。存在的意义：合并后的文档里，被上一层显式设成 <c>null</c> 的键就是字面意义上
        /// 的 <c>null</c> token；但 <c>int</c>/<c>float</c>/<c>bool</c> 这类值类型字段一旦真的
        /// 拿到 JSON <c>null</c> 去反序列化会直接抛 <see cref="JsonSerializationException"/>
        /// （"Null object cannot be converted to a value type"），而不是我们想要的"重置"效果。
        /// 把这些键整个从待反序列化的对象里去掉——即"当作它从未被设置过"——<c>ToObject</c>
        /// 就会让目标类型的构造函数把这个字段留在它自己的默认值上，等价于重置为内置默认。
        /// </summary>
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
