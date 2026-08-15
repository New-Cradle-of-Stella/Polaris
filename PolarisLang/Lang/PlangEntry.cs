using System;
using System.Collections.Generic;

namespace Polaris.Lang
{
    /// <summary>
    /// 一个 <c>.plang</c> 条目：一个 Key + 中性值（无语言命中时的兜底文案）+ 按语言代码分列的可选覆盖文案
    /// （<see cref="Values"/>，key 为语言代码，如 <c>"zh-cn"</c>/<c>"en"</c>）。
    /// </summary>
    public sealed class PlangEntry
    {
        public string Key { get; set; }

        public string Comment { get; set; }

        public string NeutralValue { get; set; } = "";

        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public PlangEntry() { }

        public PlangEntry(string key, string neutralValue, string comment = null)
        {
            Key = key;
            NeutralValue = neutralValue;
            Comment = comment;
        }
    }
}
