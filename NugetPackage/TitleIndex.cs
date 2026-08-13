using System;
using System.Collections.Generic;
using PotatoDBMapper.Models;

namespace NugetPackage;

/// <summary>
/// title表的内存索引，用来按名字反复搜索时省掉每次读库与归一化的开销。<br/>
/// 整张表只读一次，名字归一化后留在内存里。搜索时先查字典做精确匹配，绝大部分查询都能在这一步命中；
/// 没命中的才扫一遍，具体怎么比见<see cref="NameMatcher"/>。<br/>
/// 十几万个名字大约占23MB，只查一两次的话不划算，那种场景请用
/// <see cref="VnDbMapper.TryGetMapsWithName"/>，它直接扫库不建索引。
/// </summary>
internal sealed class TitleIndex
{
    /// <summary>归一化后的名字</summary>
    private readonly string[] _names;

    /// <summary>与<see cref="_names"/>一一对应的vndb id</summary>
    private readonly int[] _ids;

    /// <summary>归一化后的名字 -> <see cref="_names"/>中最后一个叫这个名字的下标，用于精确匹配</summary>
    private readonly Dictionary<string, int> _exact;

    /// <summary>
    /// 同名的上一个下标，-1表示到头了。重名的情况下这条链把它们串起来，
    /// 比给每个名字挂一个List省事得多（十几万个只装一个元素的List，光对象头就好几MB）
    /// </summary>
    private readonly int[] _sameName;

    public TitleIndex(IEnumerable<TitleModel> titles)
    {
        var names = new List<string>();
        var ids = new List<int>();
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.Title)) continue;
            var name = title.Title!.NormalizeName();
            if (name.Length == 0) continue;
            names.Add(name);
            ids.Add(title.VndbId);
        }

        _names = names.ToArray();
        _ids = ids.ToArray();

        _sameName = new int[_names.Length];
        _exact = new Dictionary<string, int>(_names.Length, StringComparer.Ordinal);
        for (var i = 0; i < _names.Length; i++)
        {
            _sameName[i] = _exact.TryGetValue(_names[i], out var previous) ? previous : -1;
            _exact[_names[i]] = i;
        }
    }

    /// <summary>
    /// 索引里一共有多少个名字
    /// </summary>
    public int Count => _names.Length;

    /// <summary>
    /// 找出名字与gameName的相似度不低于minSimilarity的所有游戏，同一个游戏只保留最像的那个名字
    /// </summary>
    public List<(int vndbId, double similarity)> Search(string gameName, double minSimilarity)
    {
        var matcher = new NameMatcher(gameName, minSimilarity);
        if (matcher.QueryIsEmpty) return matcher.Result();

        // 精确匹配上了就不用再扫了，绝大部分查询都停在这一步
        if (_exact.TryGetValue(matcher.Query, out var hit))
        {
            var exact = new List<(int vndbId, double similarity)>();
            var seen = new HashSet<int>();
            for (var i = hit; i >= 0; i = _sameName[i])
                if (seen.Add(_ids[i]))
                    exact.Add((_ids[i], 1));
            return exact;
        }

        for (var i = 0; i < _names.Length; i++)
            matcher.Add(_names[i], _ids[i]);
        return matcher.Result();
    }
}
