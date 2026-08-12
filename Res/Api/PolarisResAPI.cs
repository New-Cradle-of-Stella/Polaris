using System;
using System.Collections.Generic;

namespace Polaris.Res
{
    /// <summary>PolarisRes 的静态门面，模组代码的唯一入口。</summary>
    public static class PolarisResAPI
    {
        private static readonly Dictionary<string, ModResources> registry = new Dictionary<string, ModResources>();

        /// <summary>
        /// 取得（或创建）某模组的资源句柄。同一个 <paramref name="modId"/> 永远返回同一实例——
        /// 这不只是为了方便，PXLS 的全局 title 命名空间（M5）依赖这个单例性质来避免撞车。
        /// </summary>
        public static ModResources For(string modId)
        {
            if (string.IsNullOrEmpty(modId))
            {
                throw new ArgumentException("modId cannot be empty.", nameof(modId));
            }

            if (!registry.TryGetValue(modId, out ModResources resources))
            {
                resources = new ModResources(modId);
                registry[modId] = resources;
            }

            return resources;
        }

        /// <summary>已注册的其它模组句柄，用于跨模组借用资源；未注册返回 false。</summary>
        public static bool TryGet(string modId, out ModResources resources) => registry.TryGetValue(modId, out resources);

        // 「游戏是否就绪」「等就绪后执行」不在这里重复暴露：那是 Polaris 的游戏兼容层
        // 职责，用 Polaris.API.GameSessionRuntime.IsReady / WhenReady(...)。
        // 门面不转发上游能力，见 CLAUDE.md 的门面契约第 3 条。
    }
}
