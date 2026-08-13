using System;
using System.Collections.Generic;

namespace NugetPackage;

/// <summary>
/// 按名字找游戏时用的累加器：把候选名字一个一个喂进来，最后拿出相似度达标的游戏。<br/>
/// 名字从哪来它不管，所以走内存索引（<see cref="TitleIndex"/>）和直接扫库两条路都能用它，
/// 两边的匹配规则也就不会跑偏。
/// </summary>
internal sealed class NameMatcher
{
    /// <summary>归一化后的查询词</summary>
    private readonly string _query;

    private readonly double _minSimilarity;

    /// <summary>vndb id -> 这个游戏的若干个名字里最高的那个相似度</summary>
    private readonly Dictionary<int, double> _best = new Dictionary<int, double>();

    /// <summary>有没有名字与查询词完全一致</summary>
    private bool _exact;

    /// <summary>算编辑距离用的dp两行，所有候选共用，不够长了再换大的</summary>
    private int[] _previous = new int[64];

    private int[] _current = new int[64];

    public NameMatcher(string gameName, double minSimilarity)
    {
        _query = gameName.NormalizeName();
        _minSimilarity = minSimilarity;
    }

    /// <summary>归一化后的查询词，供索引查精确匹配用</summary>
    public string Query => _query;

    /// <summary>归一化之后是空的名字没法比，这种情况直接当查不到</summary>
    public bool QueryIsEmpty => _query.Length == 0;

    /// <summary>
    /// 喂一个候选名字进来。name必须是已经<see cref="Helper.NormalizeName"/>过的
    /// </summary>
    public void Add(string name, int vndbId)
    {
        if (name.Length == 0) return;
        int longer = Math.Max(_query.Length, name.Length), shorter = Math.Min(_query.Length, name.Length);
        // 编辑距离至少是两个名字的长度差，所以相似度最高也就到 shorter / longer，
        // 这一步连字符都不用看就能卡掉绝大部分名字
        if (shorter < _minSimilarity * longer) return;

        // similarity = 1 - distance / longer，所以相似度达到阈值等价于编辑距离不超过maxDistance
        var maxDistance = (int)((1 - _minSimilarity) * longer);
        if (_previous.Length < name.Length + 2)
        {
            _previous = new int[name.Length + 2];
            _current = new int[name.Length + 2];
        }

        var distance = Helper.BoundedLevenshtein(_query, name, maxDistance, _previous, _current);
        if (distance < 0) return;
        var similarity = 1 - distance / (double)longer;
        if (similarity < _minSimilarity) return;
        if (distance == 0) _exact = true;

        if (_best.TryGetValue(vndbId, out var exist) && exist >= similarity) return;
        _best[vndbId] = similarity;
    }

    /// <summary>
    /// 取出结果。有名字完全对上的时候只给精确匹配的那些，别再把差不多的混进来
    /// </summary>
    public List<(int vndbId, double similarity)> Result()
    {
        var result = new List<(int vndbId, double similarity)>(_best.Count);
        foreach (var pair in _best)
        {
            if (_exact && pair.Value < 1) continue;
            result.Add((pair.Key, pair.Value));
        }

        return result;
    }
}
