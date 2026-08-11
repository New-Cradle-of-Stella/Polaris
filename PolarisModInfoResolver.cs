using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Unity.Mono.Bootstrap;

namespace Polaris
{
    /// <summary>
    /// 按 dll 文件名检索 <see cref="PolarisModInfo"/>。信息来自已加载 BepInEx 插件上的
    /// <see cref="PolarisModInfoAttribute"/>（读不到特性时退化成 BepInPlugin 元数据 / 文件名），
    /// 因此只对本次游戏真正加载了的 dll 有效——被禁用成 <c>.dll.disabled</c> 的模组读不到信息。
    /// 一局游戏里插件集合不会变，结果建一次缓存长期复用；如需重新读取调用 <see cref="Invalidate"/>。
    /// </summary>
    internal static class PolarisModInfoResolver
    {
        static Dictionary<string, PolarisModInfo> byFileName;

        /// <summary>
        /// 查询 <paramref name="fileName"/>（如 "SomeMod.dll"，不含路径）对应的模组信息。
        /// 该 dll 没加载或没标特性时返回一条 <see cref="PolarisModInfo.HasModInfo"/> 为 false 的兜底记录，
        /// 不会返回 null，调用方可以直接展示 <see cref="PolarisModInfo.DisplayName"/>。
        /// </summary>
        internal static PolarisModInfo Resolve(string fileName)
        {
            byFileName ??= Build();

            return byFileName.TryGetValue(fileName, out PolarisModInfo info)
                ? info
                : new PolarisModInfo
                {
                    FileName = fileName,
                    DisplayName = Path.GetFileNameWithoutExtension(fileName),
                };
        }

        /// <summary>丢弃缓存，下次 <see cref="Resolve"/> 时重新扫描已加载插件。</summary>
        internal static void Invalidate()
        {
            byFileName = null;
        }

        /// <summary>遍历 BepInEx 已加载的插件，按 dll 文件名建索引。</summary>
        static Dictionary<string, PolarisModInfo> Build()
        {
            var map = new Dictionary<string, PolarisModInfo>(StringComparer.OrdinalIgnoreCase);

            if (UnityChainloader.Instance == null)
            {
                return map;
            }

            foreach (PluginInfo pluginInfo in UnityChainloader.Instance.Plugins.Values)
            {
                string location = pluginInfo.Location;
                if (string.IsNullOrEmpty(location))
                {
                    continue;
                }

                string fileName = Path.GetFileName(location);
                PolarisModInfoAttribute attribute = FindAttribute(pluginInfo);

                // 一个 dll 里有多个插件类时，优先保留标了特性的那条。
                if (map.TryGetValue(fileName, out PolarisModInfo existing) && (existing.HasModInfo || attribute == null))
                {
                    continue;
                }

                map[fileName] = Compose(fileName, pluginInfo.Metadata, attribute);
            }

            return map;
        }

        /// <summary>先找插件主类上的特性，没有再找它所在程序集上的；插件实例缺失或反射失败都返回 null。</summary>
        static PolarisModInfoAttribute FindAttribute(PluginInfo pluginInfo)
        {
            Type type = pluginInfo.Instance?.GetType();
            if (type == null)
            {
                return null;
            }

            try
            {
                return (PolarisModInfoAttribute)Attribute.GetCustomAttribute(type, typeof(PolarisModInfoAttribute))
                    ?? (PolarisModInfoAttribute)Attribute.GetCustomAttribute(type.Assembly, typeof(PolarisModInfoAttribute));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] Failed to read the PolarisModInfo of mod \"{pluginInfo.Metadata?.Name ?? type.FullName}\": {ex.Message}");
                return null;
            }
        }

        static PolarisModInfo Compose(string fileName, BepInPlugin metadata, PolarisModInfoAttribute attribute)
        {
            return new PolarisModInfo
            {
                FileName = fileName,
                DisplayName = Text(attribute?.DisplayName)
                    ?? Text(metadata?.Name)
                    ?? Path.GetFileNameWithoutExtension(fileName),
                Author = Text(attribute?.Author),
                Description = Text(attribute?.Description),
                Version = Text(attribute?.Version) ?? Text(metadata?.Version?.ToString()),
                Url = Text(attribute?.Url),
                HasModInfo = attribute != null,
            };
        }

        /// <summary>把空串/ 纯空白归一成 null，省得展示层到处判空白。</summary>
        static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
