using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Lang
{
    /// <summary>
    /// <c>.plang</c> 生成代码的运行时落脚点：生成的注册类在 <see cref="PlangRegistryScanner.ScanAll"/> 时
    /// 把各 Key 的文案 <see cref="Register"/> 进这里，生成的属性直接调 <see cref="Get"/> 取值。
    /// 同时把 <see cref="Get"/> 注册进 <see cref="PolarisAPI.Localization"/>，让原生 <c>XX.TX.Get(key)</c> 路径也能查到同一份文案。
    /// </summary>
    public static class PlangRuntime
    {
        sealed class Entry
        {
            public string Neutral;
            public IReadOnlyDictionary<string, string> Values;

            /// <summary>注册这个 Key 的插件程序集，用来判断重复注册算不算冲突。</summary>
            public Assembly Source;
        }

        static readonly Dictionary<string, Entry> table = new(StringComparer.Ordinal);

        /// <summary>
        /// 注册一个 Key 的文案（<paramref name="values"/> 应只含编辑器里启用的语言，语言代码大小写不敏感）。
        /// 同一 Key 被<b>另一个模组</b>再注册是致命冲突，交给 <see cref="PlangConflictGuard"/> 处理；
        /// 同程序集内部重复注册只是后者覆盖前者、记一行警告。
        /// </summary>
        public static void Register(string key, string neutralValue, IReadOnlyDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            // 扫描期间由 PlangRegistryScanner 点名；绕过扫描直接调这里时退回调用方程序集。
            Assembly source = PlangConflictGuard.CurrentSource ?? Assembly.GetCallingAssembly();

            if (table.TryGetValue(key, out Entry existing))
            {
                if (existing.Source != source)
                {
                    PlangConflictGuard.Record(key, existing.Source, source);
                    return;
                }

                Plugin.Logger.LogWarning(
                    $"[PolarisLang] Several .plang files inside {source.GetName().Name} registered the same key \"{key}\"; "
                    + "the later registration overrode the earlier one.");
            }

            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
            {
                foreach (KeyValuePair<string, string> kv in values)
                {
                    normalized[kv.Key] = kv.Value ?? "";
                }
            }

            table[key] = new Entry { Neutral = neutralValue ?? "", Values = normalized, Source = source };
        }

        /// <summary>
        /// 按 <see cref="LangSettings.EffectiveLocale"/> 取文案：先精确匹配语言代码，再退一级到 <c>-</c> 前缀，
        /// 再把默认 family <c>"_"</c> 当日文试一次，最后用中性值兜底。Key 未注册过返回 <c>null</c>（非空串），
        /// 以便 resolver 链正确放行给下一个 resolver。
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key) || !table.TryGetValue(key, out Entry entry))
            {
                return null;
            }

            string locale = LangSettings.EffectiveLocale;
            if (!string.IsNullOrEmpty(locale))
            {
                if (TryPick(entry, locale, out string exact))
                {
                    return exact;
                }

                int dash = locale.IndexOf('-');
                if (dash > 0 && TryPick(entry, locale.Substring(0, dash), out string baseLang))
                {
                    return baseLang;
                }

                // "_" 是游戏默认语言（日文）的 family key，而 .plang 里日文文案通常写作 "ja"，需要在此转译一次。
                if (locale == DefaultFamily && TryPick(entry, JapaneseCode, out string japanese))
                {
                    return japanese;
                }
            }

            return entry.Neutral;
        }

        /// <summary>游戏默认语言（日文）的 family key，见 <c>localization/___family__.txt</c>。</summary>
        const string DefaultFamily = "_";

        const string JapaneseCode = "ja";

        /// <summary>取某个语言代码下的文案；空串按"没有这一份"处理，避免采纳空白挡住后面的候选。</summary>
        static bool TryPick(Entry entry, string locale, out string value)
        {
            value = entry.Values.TryGetValue(locale, out string found) ? found : null;
            return !string.IsNullOrEmpty(value);
        }
    }
}
