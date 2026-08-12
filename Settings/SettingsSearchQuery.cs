using System;
using System.Collections.Generic;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置项搜索框的匹配规则，只做字符串判定。匹配对象一律是已按当前语言求值的显示串（非本地化键、非其它语言译文），确保玩家眼前看到的字才能搜到。
    /// </summary>
    internal static class SettingsSearchQuery
    {
        /// <summary>把查询串切成若干条件（空白分隔，AND 语义）；空数组表示无查询，即全部命中。</summary>
        internal static string[] Tokenize(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return [];
            }

            // 全角空格也当分隔符：中日文输入法常打出这个。
            string[] parts = query.ToLowerInvariant().Split([' ', '\t', '　'], StringSplitOptions.RemoveEmptyEntries);
            return parts;
        }

        /// <summary>
        /// <paramref name="haystack"/> 是否满足全部 <paramref name="tokens"/>。
        /// <paramref name="tokens"/> 为空（没有查询）时恒为 true。
        /// </summary>
        internal static bool Matches(string haystack, IReadOnlyList<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return true;
            }

            if (string.IsNullOrEmpty(haystack))
            {
                return false;
            }

            string lower = haystack.ToLowerInvariant();

            for (int i = 0; i < tokens.Count; i++)
            {
                if (!MatchesOne(lower, tokens[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>任一 <paramref name="haystacks"/> 单独满足全部条件即算命中（条件不可分散在多个串上）。</summary>
        internal static bool MatchesAny(IReadOnlyList<string> tokens, params string[] haystacks)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < haystacks.Length; i++)
            {
                if (Matches(haystacks[i], tokens))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>单个条件判定：先按子串，再退回按顺序出现即可的模糊匹配。<paramref name="haystack"/> 须已小写化。</summary>
        static bool MatchesOne(string haystack, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return true;
            }

            if (haystack.IndexOf(token, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            int t = 0;
            for (int h = 0; h < haystack.Length && t < token.Length; h++)
            {
                if (haystack[h] == token[t])
                {
                    t++;
                }
            }

            return t == token.Length;
        }
    }
}
