using System;
using System.Collections.Generic;

namespace Polaris.Localization
{
    /// <summary>
    /// 本地化 resolver 注册表 + 内置文案表，从 <see cref="PolarisAPI.Localization"/> 取。
    /// resolver 未命中须返回 <c>null</c> 而非空串，否则会被当成命中，后续 resolver 和原版查表不会再被问到。
    /// </summary>
    public sealed class LocalizationAPI
    {
        internal LocalizationAPI() { }

        readonly List<Func<string, string>> resolvers = [];

        /// <summary>内置表，<see cref="Resolve"/> 优先查询。</summary>
        readonly Dictionary<string, LocalizedText> builtin = new(StringComparer.Ordinal);

        /// <summary>注册一个 resolver；按注册顺序依次尝试，第一个返回非 null 的结果生效。</summary>
        public void RegisterResolver(Func<string, string> resolver)
        {
            if (resolver != null)
            {
                resolvers.Add(resolver);
            }
        }

        /// <summary>往内置表登记一条文案；重复登记后者覆盖前者并记警告。须在设置界面首次显示前登记完成，模组 <c>Awake</c> 里登记即可。</summary>
        public void Register(string key, LocalizedText text)
        {
            if (string.IsNullOrEmpty(key) || text == null)
            {
                return;
            }

            if (builtin.ContainsKey(key))
            {
                // ?.：不保证 Polaris 自己的 Awake（Logger 赋值处）一定先于其它模组的 Awake 跑完。
                Plugin.Logger?.LogWarning($"[Polaris] Built-in text key \"{key}\" was registered more than once; the later one wins.");
            }

            builtin[key] = text;
        }

        /// <summary>
        /// 把"显示用字符串"解析为最终文案：<c>&amp;</c> 开头查表，<c>&amp;&amp;</c> 开头脱转义，其余原样返回。
        /// 查表顺序为内置表/resolver 链 → 原版 <c>TX.Get</c> → key 本身（兜底显示 key 便于定位未登记文案）。
        /// <paramref name="raw"/> 为 null 时返回 null。
        /// </summary>
        public string Text(string raw)
        {
            if (raw == null)
            {
                return null;
            }

            if (!LocalizedString.TryGetKey(raw, out string key))
            {
                return LocalizedString.Unescape(raw);
            }

            // resolver 契约是"未命中返回 null"，空串视为有效结果直接采纳。
            string resolved = Resolve(key);
            if (resolved != null)
            {
                return resolved;
            }

            // 兼容游戏自带 key；极早期 TX 的 family 表未建好时会抛异常，须接住避免拖垮整个设置界面。
            try
            {
                string vanilla = XX.TX.Get(key);
                if (!string.IsNullOrEmpty(vanilla))
                {
                    return vanilla;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[Polaris] The vanilla lookup threw while querying localization key \"{key}\": {e.Message}");
            }

            return key;
        }

        /// <summary><see cref="Text"/> 的数组版；null 进 null 出。</summary>
        public string[] TextAll(string[] raw)
        {
            if (raw == null)
            {
                return null;
            }

            var result = new string[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                result[i] = Text(raw[i]);
            }

            return result;
        }

        /// <summary>供 <see cref="Patch.Patch_TX_Get"/> 调用；全部未命中返回 null。内置表先查，确保 Polaris 自身文案不被其它模组顶掉，且启动极早期（无 resolver 注册时）也能答出。</summary>
        internal string Resolve(string key)
        {
            if (key != null && builtin.TryGetValue(key, out LocalizedText text))
            {
                return text.Pick(CurrentLocale);
            }

            foreach (Func<string, string> resolver in resolvers)
            {
                string value;
                try
                {
                    value = resolver(key);
                }
                catch (Exception ex)
                {
                    // 一个 resolver 抛异常不应连累其它 resolver 或原版查表（Harmony Prefix 未接住会直接打断游戏本身的查询），按未命中处理并继续。
                    PolarisAPI.Errors.Report(ex, $"a localization resolver handling \"{key}\"", resolver.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError($"[Polaris] A localization resolver threw while handling \"{key}\"; skipped.");
                    continue;
                }

                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>当前语言族；启动极早期 family 表未建好时读取会抛异常，按未知语言处理（返回 null）。</summary>
        static string CurrentLocale
        {
            get
            {
                try
                {
                    return PolarisAPI.Game.Localization.CurrentLocale;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}
