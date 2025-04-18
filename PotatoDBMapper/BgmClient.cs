using Newtonsoft.Json.Linq;

namespace PotatoDBMapper;

public class BgmClient
{
    private readonly List<BgmElement> _games = new();

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

    public async Task<(int, int, float percent)> GetId(string name)
    {
        var minDistance = int.MaxValue;
        var target = new BgmElement();
        foreach (var game in _games)
        {
            var d1 = name.Levenshtein(game.NameCn);
            var d2 = name.Levenshtein(game.Name);
            if (d1 < minDistance || d2 < minDistance)
            {
                minDistance = Math.Min(d1, d2);
                target = game;
                if(minDistance == 0) break;
            }
        }

        await Task.CompletedTask;
        return minDistance == int.MaxValue
            ? (-1, int.MaxValue, 0f)
            : (target.Id, minDistance, 1 - (float)minDistance / Math.Max(target.Name?.Length ?? 999, name.Length));
    }
    
    public BgmClient()
    {
        Init();
    }
    public List<BgmElement> GetGames => _games;
}

public struct BgmElement
{
    public string NameCn;
    public string Name;
    public int Id;
}