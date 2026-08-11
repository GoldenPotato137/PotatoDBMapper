using Newtonsoft.Json.Linq;
using PotatoDBMapper.Utils;

namespace PotatoDBMapper;

public class BgmClient
{
    /// <summary>
    /// 相似度低于此值的匹配结果会被丢弃
    /// </summary>
    private const float SimilarityThreshold = 0.7f;

    private readonly List<BgmElement> _games = new();
    private readonly NameIndex _index;

    private void Init()
    {
        Console.WriteLine("Loading Bgm database...");
        using var reader = new StreamReader("./assets/input/subject.jsonlines");
        while (reader.ReadLine() is { } element)
        {
            var jsonToken = JToken.Parse(element);
            if (jsonToken["type"]!.ToObject<int>() != 4) continue;
            _games.Add(new BgmElement
            {
                NameCn = jsonToken["name_cn"]!.ToObject<string>()!,
                Name = jsonToken["name"]!.ToObject<string>()!,
                Id = jsonToken["id"]!.ToObject<int>()
            });
        }
    }

    /// <summary>
    /// 每个游戏的原名与中文名都拿来建索引
    /// </summary>
    private IEnumerable<(string name, int id)> GetIndexedNames()
    {
        foreach (var game in _games)
        {
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
        Console.WriteLine($"Loaded {_games.Count} bgm games, building index...");
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
