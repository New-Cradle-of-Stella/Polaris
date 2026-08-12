using System;

namespace Polaris.Event
{
    /// <summary>把哈++事件的命名空间 + 逻辑 ID 换算成注入 <c>EV</c> 内容表用的运行时键。</summary>
    public static class PolarisEventId
    {
        /// <summary>
        /// <c>%</c> 前缀是保留/内存事件的既有约定——<c>EV.clearEventContent()</c> 清理普通缓存时会跳过它，
        /// 见 PolarisEvent-实现计划.md §4.1。
        /// </summary>
        public const string Prefix = "%polaris/";

        public static string BuildRuntimeKey(string @namespace, string logicalId)
        {
            if (string.IsNullOrEmpty(@namespace))
            {
                throw new ArgumentException("Namespace cannot be empty.", nameof(@namespace));
            }

            if (string.IsNullOrEmpty(logicalId))
            {
                throw new ArgumentException("Logical id cannot be empty.", nameof(logicalId));
            }

            return Prefix + @namespace + "/" + logicalId;
        }

        /// <summary>运行时键比较采用 ordinal ignore-case，与作者侧命令和别名体验一致（实现计划 §4.1 冻结决策）。</summary>
        public static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
    }
}
