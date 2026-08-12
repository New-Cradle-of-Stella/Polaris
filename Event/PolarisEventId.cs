using System;

namespace Polaris.Event
{
    /// <summary>把哈++事件的命名空间 + 逻辑 ID 换算成注入 <c>EV</c> 内容表用的运行时键。</summary>
    public static class PolarisEventId
    {
        /// <summary>The <c>%</c> prefix marks a reserved/in-memory event, which <c>EV.clearEventContent()</c> skips when clearing the normal cache.</summary>
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

        /// <summary>Runtime key comparisons are ordinal, case-insensitive.</summary>
        public static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
    }
}
