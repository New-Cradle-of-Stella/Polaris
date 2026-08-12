using System;
using System.Collections.Generic;

namespace Polaris.Event.Compiler.Binding
{
    /// <summary>Levenshtein 编辑距离拼写建议，对应 HPP2103 "你是否想写：Noel.Happy？" 这类诊断。</summary>
    public static class SpellingSuggestions
    {
        public static string Suggest(IEnumerable<string> candidates, string input, int maxDistance = 2)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            string best = null;
            int bestDistance = int.MaxValue;
            foreach (var candidate in candidates)
            {
                int distance = Levenshtein(candidate, input);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return bestDistance <= maxDistance ? best : null;
        }

        static int Levenshtein(string a, string b)
        {
            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++)
            {
                dp[i, 0] = i;
            }

            for (int j = 0; j <= b.Length; j++)
            {
                dp[0, j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
                }
            }

            return dp[a.Length, b.Length];
        }
    }
}
