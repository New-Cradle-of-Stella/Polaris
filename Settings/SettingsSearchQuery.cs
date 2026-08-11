using System;
using System.Collections.Generic;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置项搜索框的匹配规则。<b>只做字符串判定</b>，不认识 nel/XX/Unity 的任何类型——
    /// 界面那一半在 <see cref="SettingsSearchFilter"/> 与 <see cref="SettingsSearchBox"/>。
    /// <para>
    /// 匹配的对象一律是<b>已经按当前语言求过值的显示串</b>（<c>DisplayTitle</c>/<c>DisplayLabel</c>/
    /// <c>DisplayDescription</c>），不是 <c>&amp;polaris.xxx</c> 这种本地化键，也不是别的语言的译文：
    /// 玩家眼前看到的是哪几个字，就该能用哪几个字搜到。反过来说，中文界面下输入英文原名是搜不到的
    /// ——那是刻意的，否则"能搜到但屏幕上找不到那一行"会更让人困惑。
    /// </para>
    /// </summary>
    internal static class SettingsSearchQuery
    {
        /// <summary>
        /// 把查询串切成若干条件。空白分隔，全部条件都命中才算命中（AND），
        /// 这样"polaris 版本"能同时用模组名和设置名收敛。
        /// <para>
        /// 返回空数组表示"没有查询"，调用方应当把所有项都视为命中。
        /// </para>
        /// </summary>
        internal static string[] Tokenize(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return [];
            }

            // 全角空格也当分隔符：中日文输入法下打出来的空格多半是这一个。
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

        /// <summary>
        /// 任意一个 <paramref name="haystacks"/> 满足全部条件即算命中。
        /// 注意不是"条件可以分散在几个串上"——那样"版本 行"会把只在说明里出现"行"的项也捞进来。
        /// </summary>
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

        /// <summary>
        /// 单个条件的判定：先按子串，再退回"按顺序出现即可"的模糊匹配
        /// （输入 "标版" 能命中 "标题画面版本行"）。
        /// <para>
        /// <paramref name="haystack"/> 必须已经小写化——这个函数在每一项上都会被调到，
        /// 把大小写归一提到外面做，一次查询就只归一一遍。
        /// </para>
        /// </summary>
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
