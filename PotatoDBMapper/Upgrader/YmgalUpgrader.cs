using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using PotatoDBMapper.Models;
using PotatoDBMapper.Utils;
using SQLite;
using ProgressBar = ShellProgressBar.ProgressBar;

namespace PotatoDBMapper.Upgrader;

/// <summary>
/// 基于月幕Galgame（ymgal）的离线数据包来更新映射。<br/>
/// 数据来自 https://github.com/ymgal/data-export ，字段含义见 https://www.ymgal.games/developer 。<br/>
/// ymgal的游戏名（原名、中文名、别名）和vndb的游戏名重合度很高，所以直接拿title表建索引来匹配，
/// 具体做法见<see cref="NameIndex"/>。
/// </summary>
public class YmgalUpgrader(SQLiteAsyncConnection connection, string[] args)
{
    private const string GameFile = "./assets/input/ymgal_game.jsonl";

    /// <summary>
    /// 相似度低于此值的匹配结果会被丢弃
    /// </summary>
    private const float SimilarityThreshold = 0.7f;

    private readonly bool _updateMap = args.Contains("skip-update-map") == false;
    private readonly bool _updateTitle = args.Contains("skip-update-title") == false;

    public async Task Upgrade()
    {
        if (File.Exists(GameFile) == false)
        {
            Console.WriteLine($"{GameFile} not found, skip ymgal mapping. " +
                              "You can run ./assets/input/fetch_ymgal.sh to download it.");
            return;
        }

        Console.WriteLine("Loading Ymgal games...");
        var games = LoadGames();
        Console.WriteLine($"Loaded {games.Count} ymgal games.");
        Console.WriteLine("Loading Game Titles...");
        var titles = await connection.Table<TitleModel>().ToListAsync();
        var index = new NameIndex(titles.Select(title => (title.Title!, title.VndbId)));

        // vndbId -> 此vndb游戏匹配到的最佳ymgal游戏
        var matches = new Dictionary<int, YmgalMatch>();
        // ymgal的中文名 -> vndbId，只包含title表里还没有的名字
        var newTitles = new Dictionary<string, int>();
        using (var pbar = new ProgressBar(games.Count, "Start matching ymgal games...", Utils.ProgressBar.Options))
        {
            Console.WriteLine("Start matching ymgal games...");
            Parallel.ForEach(games, () => new MatchState(index),
                (game, _, state) =>
                {
                    try
                    {
                        Match(game, index, state);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to match gid {game.Gid} with error: {e.Message}");
                    }

                    pbar.Tick($"Matching gid: {game.Gid} {game.Name}");
                    return state;
                },
                state =>
                {
                    lock (matches) MergeTo(state, matches, newTitles);
                });
        }

        Console.WriteLine($"Matched {matches.Count} vndb games to ymgal.");

        if (_updateTitle && newTitles.Count > 0)
        {
            Console.WriteLine($"Adding {newTitles.Count} titles from ymgal...");
            await connection.RunInTransactionAsync(db =>
            {
                foreach (var (title, vndbId) in newTitles)
                    db.InsertOrReplace(new TitleModel { Title = title, VndbId = vndbId });
            });
        }

        if (_updateMap == false) return;
        Console.WriteLine("Updating vn_mapper.db...");
        await connection.RunInTransactionAsync(db =>
        {
            foreach (var (vndbId, match) in matches)
            {
                var map = db.Find<MapModel>(vndbId) ?? new MapModel(vndbId);
                map.YmgalId = match.Gid;
                map.YmgalDistance = match.Distance;
                map.YmgalSimilarity = match.Similarity;
                db.InsertOrReplace(map);
            }

            // 加字段之前就存在的行在新字段上是null，统一成“没匹配上”的默认值，方便直接用sql查
            db.Execute("update map set YmgalId = 0, YmgalDistance = ?, YmgalSimilarity = 0 where YmgalId is null",
                int.MaxValue);
        });
    }

    private static void Match(YmgalGame game, NameIndex index, MatchState state)
    {
        var exactVndbId = -1;
        foreach (var rawName in game.GetNames())
        {
            foreach (var match in state.Matcher.Match(rawName, SimilarityThreshold))
            {
                Add(state.Matches, match.Id, new YmgalMatch(game.Gid, match.Distance, match.Similarity));
                if (match.Distance == 0 && exactVndbId < 0) exactVndbId = match.Id;
            }
        }

        // 类似BgmUpgrader，把精确匹配上的游戏的ymgal中文名也补充进title表，方便按名字查询。
        // title表中已经存在的名字不会被覆盖，避免弄坏vndb本身的数据。
        if (exactVndbId < 0 || string.IsNullOrWhiteSpace(game.ChineseName)) return;
        var chineseName = game.ChineseName!.Trim();
        if (index.Contains(NameIndex.Normalize(chineseName))) return;
        state.NewTitles[chineseName] = exactVndbId;
    }

    private static void MergeTo(MatchState state, Dictionary<int, YmgalMatch> matches, Dictionary<string, int> newTitles)
    {
        foreach (var (vndbId, match) in state.Matches)
            Add(matches, vndbId, match);

        foreach (var (title, vndbId) in state.NewTitles)
        {
            if (newTitles.TryGetValue(title, out var exist) && exist <= vndbId) continue;
            newTitles[title] = vndbId;
        }
    }

    private static void Add(Dictionary<int, YmgalMatch> matches, int vndbId, YmgalMatch match)
    {
        // 相似度更高的结果更好；相似度相同时取gid较小的，好让多次运行的结果保持一致
        if (matches.TryGetValue(vndbId, out var exist) &&
            (match.Similarity < exist.Similarity ||
             (match.Similarity == exist.Similarity && match.Gid >= exist.Gid))) return;
        matches[vndbId] = match;
    }

    private static List<YmgalGame> LoadGames()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = new List<YmgalGame>();
        using var reader = new StreamReader(GameFile);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            YmgalGame? game = null;
            try
            {
                game = JsonSerializer.Deserialize<YmgalGame>(line, options);
            }
            catch (JsonException e)
            {
                Console.WriteLine($"Failed to parse ymgal game with error: {e.Message}");
            }

            if (game is { Gid: > 0 }) result.Add(game);
        }

        return result;
    }

    private readonly record struct YmgalMatch(int Gid, int Distance, float Similarity);

    /// <summary>
    /// 单个线程的匹配状态
    /// </summary>
    private sealed class MatchState(NameIndex index)
    {
        public readonly NameIndex.Matcher Matcher = index.CreateMatcher();
        public readonly Dictionary<int, YmgalMatch> Matches = new();
        public readonly Dictionary<string, int> NewTitles = new();
    }
}

#region YMGAL_MODEL

/// <summary>
/// ymgal的游戏档案。注意离线数据包里的字段名是下划线风格的，和开放API的驼峰风格不一样
/// </summary>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class YmgalGame
{
    [JsonPropertyName("gid")] public int Gid { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("chinese_name")] public string? ChineseName { get; set; }

    [JsonPropertyName("extension_name")] public List<YmgalExtensionName>? ExtensionName { get; set; }

    /// <summary>
    /// 用于匹配的所有名字：原名、中文名、以及别名、各语言译名
    /// </summary>
    public IEnumerable<string> GetNames()
    {
        if (string.IsNullOrWhiteSpace(Name) == false) yield return Name!;
        if (string.IsNullOrWhiteSpace(ChineseName) == false) yield return ChineseName!;
        if (ExtensionName is null) yield break;
        foreach (var extension in ExtensionName)
            if (string.IsNullOrWhiteSpace(extension.Name) == false)
                yield return extension.Name!;
    }
}

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class YmgalExtensionName
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

#endregion
