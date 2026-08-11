using System;

namespace NugetPackage;

public static class Helper
{
    /// <summary>
    /// 计算两个字符串的编辑距离
    /// </summary>
    public static int Levenshtein(this string s1, string s2)
    {
        s1 = s1.ToLower();
        s2 = s2.ToLower();
        int n = s1.Length, m = s2.Length;
        var dp = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++)
            dp[i, 0] = i;
        for (var j = 0; j <= m; j++)
            dp[0, j] = j;
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                dp[i, j] = Math.Min(dp[i - 1, j], dp[i, j - 1]) + 1;
                dp[i, j] = Math.Min(dp[i, j], dp[i - 1, j - 1] + (s1[i - 1] == s2[j - 1] ? 0 : 1));
            }
        }

        return dp[n, m];
    }

    public static double Similarity(this string s1, string s2)
    {
        return 1 - 1f * Levenshtein(s1, s2) / Math.Max(s1.Length, s2.Length);
    }
}