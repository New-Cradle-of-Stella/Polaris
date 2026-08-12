using System;
using System.Collections.Generic;

namespace Polaris.Localization
{
    /// <summary>
    /// 一条内置文案：一份兜底的中性文本 + 若干语言的覆盖。用集合初始化器写最顺手：
    /// <code>
    /// new LocalizedText("Strict mode")
    /// {
    ///     ["zh"] = "严格模式",
    ///     ["ja"] = "厳格モード",
    /// }
    /// </code>
    /// <para>
    /// 这是给<b>没法用 <c>.plang</c> 的场合</b>准备的：Polaris 自己的设置项标签在
    /// <c>.plang</c> 运行时起来之前（<c>Plugin.Awake</c> 阶段）就要能查到，文案却一样要
    /// 跟着玩家的语言走。给玩家装的模组仍然应该用 <c>.plang</c>——那边有编辑器、
    /// 有 key 冲突检查，也不必把文案写进代码。
    /// </para>
    /// <para>
    /// 语言代码建议与 <c>PolarisAPI.Game.CurrentLocale</c>（<c>"_"</c>/<c>"en"</c>/
    /// <c>"zh-cn"</c>/<c>"ko-kr"</c>……）对齐，大小写不敏感。取值规则见 <see cref="Pick"/>：
    /// 只写一个 <c>"zh"</c> 就能同时覆盖 <c>zh-cn</c> 与 <c>zh-tw</c>，不必逐个方言写一遍。
    /// </para>
    /// </summary>
    public sealed class LocalizedText
    {
        readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        /// <param name="neutral">
        /// 所有语言都没匹配上时显示的文本。建议填英文：Polaris 的内置文案一律以英文兜底
        /// （与 <see cref="Diagnostics.FatalText"/>、标题告知页的处理一致）。
        /// </param>
        public LocalizedText(string neutral) => Neutral = neutral ?? "";

        /// <summary>兜底文本，永远非 null。</summary>
        public string Neutral { get; }

        /// <summary>某个语言代码下的覆盖文案；没有登记过读出来是 null。</summary>
        public string this[string locale]
        {
            get => locale != null && values.TryGetValue(locale, out string v) ? v : null;
            set
            {
                if (!string.IsNullOrEmpty(locale) && value != null)
                {
                    values[locale] = value;
                }
            }
        }

        /// <summary>
        /// 按语言代码取文案：精确匹配 → 按 <c>-</c> 退一级（<c>"zh-cn"</c> 退到 <c>"zh"</c>）
        /// → 游戏默认语言 <c>"_"</c> 视同日文再试一次 → <see cref="Neutral"/>。
        /// <para>
        /// 那个 <c>"_"</c> 是游戏自己的默认 family key（见 <c>PolarisAPI.Game.Localization.CurrentLocale</c>），
        /// 语义上就是日文；不在这里翻译一次的话，玩日文版的玩家会拿到英文兜底——而这是
        /// 绝大多数玩家的默认设置。
        /// </para>
        /// </summary>
        internal string Pick(string locale)
        {
            if (string.IsNullOrEmpty(locale) || values.Count == 0)
            {
                return Neutral;
            }

            if (values.TryGetValue(locale, out string exact))
            {
                return exact;
            }

            int dash = locale.IndexOf('-');
            if (dash > 0 && values.TryGetValue(locale.Substring(0, dash), out string baseLang))
            {
                return baseLang;
            }

            if (locale == DefaultFamily && values.TryGetValue("ja", out string japanese))
            {
                return japanese;
            }

            return Neutral;
        }

        /// <summary>游戏默认语言（日文）的 family key，见 <c>localization/___family__.txt</c>。</summary>
        internal const string DefaultFamily = "_";
    }
}
