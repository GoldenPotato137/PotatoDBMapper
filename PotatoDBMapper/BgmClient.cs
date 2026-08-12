using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using PotatoDBMapper.Utils;

namespace PotatoDBMapper;

public partial class BgmClient
{
    /// <summary>
    /// 相似度低于此值的匹配结果会被丢弃
    /// </summary>
    private const float SimilarityThreshold = 0.7f;

    private readonly List<BgmElement> _games = new();
    private readonly Dictionary<int, List<BgmElement>> _vndbLinks = new();

    /// <summary>infobox里填了vndb链接的那些bgm条目的id，见<see cref="GetIndexedNames"/></summary>
    private readonly HashSet<int> _linked = new();

    private readonly NameIndex _index;

    /// <summary>bangumi条目的infobox里填的vndb链接，形如 [VNDB|https://vndb.org/v10680]</summary>
    [GeneratedRegex(@"vndb\.org/v(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VndbLinkRegex();

    private void Init()
    {
        Console.WriteLine("Loading Bgm database...");
        using var reader = new StreamReader("./assets/input/subject.jsonlines");
        while (reader.ReadLine() is { } element)
        {
            var jsonToken = JToken.Parse(element);
            if (jsonToken["type"]!.ToObject<int>() != 4) continue;
            var game = new BgmElement
            {
                NameCn = jsonToken["name_cn"]!.ToObject<string>()!,
                Name = jsonToken["name"]!.ToObject<string>()!,
                Id = jsonToken["id"]!.ToObject<int>()
            };
            _games.Add(game);

            // bangumi的编辑往往在infobox里填了对应的vndb页面，这种现成的关联比按名字猜可靠得多
            var infobox = jsonToken["infobox"]?.ToObject<string>();
            if (string.IsNullOrEmpty(infobox)) continue;
            foreach (var match in VndbLinkRegex().Matches(infobox))
            {
                if (int.TryParse(((Match)match).Groups[1].ValueSpan, out var vndbId) == false) continue;
                _linked.Add(game.Id);
                if (_vndbLinks.TryGetValue(vndbId, out var linked))
                {
                    if (linked.Exists(x => x.Id == game.Id) == false) linked.Add(game);
                }
                else _vndbLinks[vndbId] = [game];
            }
        }
    }

    /// <summary>
    /// 取bangumi自己填了vndb链接的那个条目。一个vndb游戏被多个bangumi条目关联时（比如按分卷拆开的），
    /// 优先取名字和vndb对得上的，其次取名字最短的（一般是本篇），最后按id定序
    /// </summary>
    public (int id, int distance, float similarity)? GetLinkedId(int vndbId, IEnumerable<string> vndbNames)
    {
        if (_vndbLinks.TryGetValue(vndbId, out var linked) == false) return null;
        var wanted = new HashSet<string>();
        foreach (var name in vndbNames)
            if (string.IsNullOrEmpty(name) == false)
                wanted.Add(NameIndex.Normalize(name));

        BgmElement? best = null;
        var bestExact = false;
        foreach (var game in linked)
        {
            var exact = Matches(game.Name) || Matches(game.NameCn);
            bool Matches(string? name) =>
                string.IsNullOrEmpty(name) == false && wanted.Contains(NameIndex.Normalize(name));
            if (best is { } current &&
                (exact, -(game.Name?.Length ?? 0), -game.Id).CompareTo((bestExact, -(current.Name?.Length ?? 0), -current.Id)) <= 0)
                continue;
            best = game;
            bestExact = exact;
        }

        // 映射来自现成的关联，不是靠名字猜的，所以按"完全可信"记
        return best is { } result ? (result.Id, 0, 1f) : null;
    }

    /// <summary>
    /// 每个游戏的原名与中文名都拿来建索引。<br/>
    /// 自己在infobox里填了vndb链接的条目不进索引：它对应哪个vndb游戏是编辑写明的，那个游戏走
    /// <see cref="GetLinkedId"/>就拿到了，而别的vndb游戏再按名字撞上来必然是错的
    /// （「ぴゅあぴゅあ電撃姫オリジナル」撞上写明了自己是v1272的「ぴゅあぴゅあ」这种）
    /// </summary>
    private IEnumerable<(string name, int id)> GetIndexedNames()
    {
        foreach (var game in _games)
        {
            if (_linked.Contains(game.Id)) continue;
            yield return (game.Name, game.Id);
            yield return (game.NameCn, game.Id);
        }
    }

    /// <summary>
    /// 找出名字与给定名字最相似的bgm游戏，没有足够相似的则返回(-1, int.MaxValue, 0)
    /// </summary>
    public (int id, int distance, float similarity) GetId(string name)
    {
        var match = _index.MatchBest(name, SimilarityThreshold);
        return match is { } result
            ? (result.Id, result.Distance, result.Similarity)
            : (-1, int.MaxValue, 0f);
    }

    public BgmClient()
    {
        Init();
        Console.WriteLine($"Loaded {_games.Count} bgm games ({_linked.Count} of them have a vndb link " +
                          "in their infobox and are matched by that link only), building index...");
        _index = new NameIndex(GetIndexedNames());
    }

    public List<BgmElement> GetGames => _games;
}

public struct BgmElement
{
    public string NameCn;
    public string Name;
    public int Id;
}
