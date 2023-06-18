namespace PotatoDBMapper;

public static class Helper
{
    /// <summary>
    /// 计算两个字符串的相似度
    /// </summary>
    /// <returns>jaro-winkler距离: [0,1]</returns>
    public static double Similarity(this string s1, string s2)
    {
        s1 = s1.ToLower();
        s2 = s2.ToLower();
        if(s1.Length > s2.Length)
            (s1, s2) = (s2, s1);
        int n = s1.Length, m = s2.Length, range = Math.Max(m / 2 - 1, 0);
        int match = 0, swap = 0;
        for (var i = 0; i < n; i++)
        {
            if (s1[i] == s2[i])
            {
                match++;
                continue;
            }
            int matched = 0, swapped = 0;
            for(var j = Math.Max(0, i-range); j < Math.Min(m, i+range+1); j++)
                if (i!= j && s1[i] == s2[j])
                {
                    matched = 1;
                    if (i != j && j < n && s1[j] == s2[i])
                        swapped = 1;
                    if(swapped == 1)
                        break;
                }
            swap += swapped;
            match += matched;
        }
        if (match == 0)
            return 0;
        return (match / (double)n + match / (double)m + (match - swap / 2.0) / match) / 3.0;
    }   
}