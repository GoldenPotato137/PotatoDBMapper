using System;
using System.Text;

namespace NugetPackage;

/// <summary>
/// 比较游戏名用的字符串工具
/// </summary>
public static class Helper
{
    /// <summary>
    /// 计算两个字符串的编辑距离
    /// </summary>
    public static int Levenshtein(this string s1, string s2)
    {
        if (s1 == null) throw new ArgumentNullException(nameof(s1));
        if (s2 == null) throw new ArgumentNullException(nameof(s2));
        s1 = s1.ToLower();
        s2 = s2.ToLower();
        // 编辑距离最多就是长的那个的长度（整串换掉），拿它当上界等价于不设上界
        var maxDistance = Math.Max(s1.Length, s2.Length);
        return BoundedLevenshtein(s1, s2, maxDistance, new int[s2.Length + 2], new int[s2.Length + 2]);
    }

    /// <summary>
    /// 计算两个字符串的相似度：<c>1 - 编辑距离 / max(两个字符串的长度)</c>，取值[0,1]，越大越像
    /// </summary>
    public static double Similarity(this string s1, string s2)
    {
        var maxLength = Math.Max(s1.Length, s2.Length);
        if (maxLength == 0) return 1;
        return 1 - 1.0 * Levenshtein(s1, s2) / maxLength;
    }

    /// <summary>
    /// 归一化游戏名：全角转半角、统一大小写、去掉空白，并统一常见的波浪号与连字符写法。<br/>
    /// 建库时两边的名字都是这么处理过再比的，所以查询时也要用同一套规则
    /// </summary>
    public static string NormalizeName(this string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        try
        {
            name = name.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            // 少数脏数据可能不是合法的unicode，直接用原名
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c)) continue;
            builder.Append(c switch
            {
                '〜' or '～' or '∼' or '⁓' => '~',
                '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' => '-',
                _ => c
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// 计算两个字符串的编辑距离，只填dp矩阵中主对角线附近宽为maxDistance的带（Ukkonen），
    /// 距离超过maxDistance时直接返回-1，不必算出准确值。<br/>
    /// dp的两行由调用方传进来，好让批量比较时复用同一对数组，别反复分配。
    /// 两个数组的长度都要不小于<c>s2.Length + 2</c>
    /// </summary>
    internal static int BoundedLevenshtein(string s1, string s2, int maxDistance, int[] previous, int[] current)
    {
        int n = s1.Length, m = s2.Length;
        if (Math.Abs(n - m) > maxDistance) return -1;
        // 有一边是空串时下面的带是空的，单独处理掉：距离就是另一边的长度
        if (n == 0 || m == 0) return Math.Max(n, m);

        // 编辑距离超过maxDistance的路径不必区分具体值，一律当成无穷大
        var infinity = maxDistance + 1;
        for (var j = 0; j <= m; j++)
            previous[j] = j;

        for (var i = 1; i <= n; i++)
        {
            int from = Math.Max(1, i - maxDistance), to = Math.Min(m, i + maxDistance);
            current[from - 1] = from == 1 ? i : infinity;
            if (to < m) current[to + 1] = infinity; // 带外的格子会被下一行读到
            var rowMin = int.MaxValue;
            for (var j = from; j <= to; j++)
            {
                var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + (s1[i - 1] == s2[j - 1] ? 0 : 1));
                current[j] = value;
                if (value < rowMin) rowMin = value;
            }

            if (rowMin > maxDistance) return -1; // 整行都超了，后面不可能再降回来
            var swap = previous;
            previous = current;
            current = swap;
        }

        var distance = previous[m];
        return distance > maxDistance ? -1 : distance;
    }
}
