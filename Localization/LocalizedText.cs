using System;
using System.Collections.Generic;

namespace Polaris.Localization
{
    /// <summary>
    /// 一条内置文案：兜底中性文本 + 若干语言覆盖，供不便用 <c>.plang</c> 的场合（如启动早期即需可查的文案）使用。
    /// 语言代码建议对齐 <c>PolarisAPI.Game.CurrentLocale</c>，大小写不敏感；取值规则见 <see cref="Pick"/>。
    /// </summary>
    public sealed class LocalizedText
    {
        readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        /// <param name="neutral">所有语言都未匹配时显示的兜底文本，建议填英文。</param>
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
        /// 按语言代码取文案：精确匹配 → 按 <c>-</c> 退一级（<c>"zh-cn"</c>→<c>"zh"</c>）→ 游戏默认语言 <c>"_"</c> 视同日文 → <see cref="Neutral"/>。
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
