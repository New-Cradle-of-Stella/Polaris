using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Lang
{
    /// <summary>
    /// <c>.plang</c> 生成代码的运行时落脚点，取代旧版 <c>LangLoader</c> 的运行时目录扫描。
    /// 生成的 <c>{File}_PlangRegistrar</c>（见 <see cref="PlangAutoRegistrationAttribute"/>）在
    /// <see cref="PlangRegistryScanner.ScanAll"/> 时把每个 Key 的中性值/各语言文案
    /// <see cref="Register"/> 进这里；生成的只读属性直接调 <see cref="Get"/> 取值，不再经过
    /// <c>XX.TX.Get</c>/Harmony patch 那一整条链路。
    /// <para>
    /// 同时把 <see cref="Get"/> 注册进 <see cref="PolarisAPI.Localization"/>（见
    /// <c>Plugin.Init</c>），这样任何直接调用原生 <c>XX.TX.Get(key)</c> 的代码（比如 PUI
    /// 的 <c>&amp;key</c> 语法）也能查到同一份文案，两条路径结果始终一致。
    /// </para>
    /// </summary>
    public static class PlangRuntime
    {
        sealed class Entry
        {
            public string Neutral;
            public IReadOnlyDictionary<string, string> Values;

            /// <summary>注册这个 Key 的插件程序集，用来判断"又有人注册同一个 Key"算不算冲突。</summary>
            public Assembly Source;
        }

        static readonly Dictionary<string, Entry> table = new(StringComparer.Ordinal);

        /// <summary>
        /// 注册一个 Key 的文案。<paramref name="values"/> 只应该包含编辑器里"启用"的语言——
        /// 由生成代码保证，这里不做二次过滤。语言代码大小写不敏感（内部按
        /// <see cref="StringComparer.OrdinalIgnoreCase"/> 存）。
        /// <para>
        /// 同一个 Key 被<b>另一个模组</b>再注册一次是致命错误：先注册的那份文案保留，冲突交给
        /// <see cref="PlangConflictGuard"/>（最终会写出报告并请玩家退出游戏，理由见那里）。
        /// 同一个程序集内部的重复注册不算冲突——那是同一个作者的两份 <c>.plang</c> 用了同一个
        /// Key，只影响他自己的模组，照旧后者覆盖前者，记一行警告就够了。
        /// </para>
        /// </summary>
        public static void Register(string key, string neutralValue, IReadOnlyDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            // 扫描期间由 PlangRegistryScanner 点名（准确、且不受内联/优化影响）；有人绕过扫描
            // 直接调这里时退回调用方程序集。
            Assembly source = PlangConflictGuard.CurrentSource ?? Assembly.GetCallingAssembly();

            if (table.TryGetValue(key, out Entry existing))
            {
                if (existing.Source != source)
                {
                    PlangConflictGuard.Record(key, existing.Source, source);
                    return;
                }

                Plugin.Logger.LogWarning(
                    $"[PolarisLang] {source.GetName().Name} 内有多份 .plang 注册了同一个 key「{key}」，"
                    + "后注册的覆盖了先注册的。");
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
        /// 按 <see cref="LangSettings.EffectiveLocale"/>（玩家指定的语言，或"自动"时的
        /// <see cref="PolarisAPI.Game.CurrentLocale"/>，形如 <c>"zh-cn"</c>/<c>"en"</c>）取文案：
        /// 先精确匹配语言代码，匹配不到再按 <c>-</c> 前缀退一级（<c>"zh-cn"</c> 退到 <c>"zh"</c>），
        /// 再不行就把游戏的默认 family <c>"_"</c> 当日文试一次，最后用中性值兜底。
        /// Key 整体没注册过返回 <c>null</c>（不是空串）——把"这个 key 不归 PlangRuntime 管"和
        /// "这个 key 确实是空文案"区分开，好让 <see cref="Localization.LocalizationAPI"/> 的
        /// resolver 链正确放行给下一个 resolver/原版查表。
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

                // "_" 是游戏默认语言（日文）的 family key，而 .plang 作者在编辑器里填的是语言
                // 代码，日文那一列几乎都写成 "ja"。不在这里翻译一次的话，玩默认设置（也就是
                // 绝大多数日文玩家）会拿到中性值，明明有日文译文却看不到。
                // 排在精确匹配之后：作者真的用 "_" 当代码时，上面那一步已经命中了。
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

        /// <summary>
        /// 取某个语言代码下的文案。空串按"没有这一份"处理：编辑器里留空的格子会照样生成进
        /// 表里，采纳它等于把界面画成空白，还挡住了后面的候选。
        /// </summary>
        static bool TryPick(Entry entry, string locale, out string value)
        {
            value = entry.Values.TryGetValue(locale, out string found) ? found : null;
            return !string.IsNullOrEmpty(value);
        }
    }
}
