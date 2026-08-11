using System;
using System.Collections.Generic;

namespace Polaris.Lang
{
    /// <summary>
    /// 一个 <c>.plang</c> 条目的内存表示：一个 Key + 中性值（兜底文案）+ 按语言代码分列的文案。
    /// <see cref="NeutralValue"/> 是唯一必填的文案——没有任何语言命中时的兜底，语义等价于 v1
    /// 时代唯一的 <c>Value</c>；<see cref="Values"/> 是可选的按语言覆盖，key 是语言代码
    /// （如 <c>"zh-cn"</c>/<c>"en"</c>，建议跟 <c>PolarisAPI.Game.CurrentLocale</c> 的取值对齐）。
    /// <para>
    /// 所有文案一律按"可以换行的长文本"处理（v2 里每份文案都是自己的 CDATA 子节点），没有
    /// 短/长之分——旧版那个 <c>Type</c> 字段只影响 XML 里写属性还是写子节点，对使用者是纯噪音，
    /// 已经去掉。读旧文件时 <see cref="PlangDocument"/> 仍认得 v1 的 <c>Type="Long"</c>。
    /// </para>
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
