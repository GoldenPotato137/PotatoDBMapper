using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PotatoDBMapper.Models;
using PotatoDBMapper.Utils;
using SQLite;
using ProgressBar = ShellProgressBar.ProgressBar;

namespace PotatoDBMapper.Upgrader;

/// <summary>
/// 基于steam的游戏列表来更新映射，匹配方式见<see cref="NameIndex"/>
/// </summary>
public class SteamUpgrader(SQLiteAsyncConnection connection)
{
    private const string AppListFile = "./assets/input/steam.json";

    /// <summary>
    /// 相似度低于此值的匹配结果会被丢弃
    /// </summary>
    private const float SimilarityThreshold = 0.7f;

    public async Task Upgrade()
    {
        if (File.Exists(AppListFile) == false)
        {
            Console.WriteLine($"{AppListFile} not found, skip steam mapping. " +
                              "You can run ./assets/input/fetch_steam.sh to download it.");
            return;
        }

        Console.WriteLine("Loading Steam Game Lists...");
        List<SteamApp>? apps = null;
        try
        {
            var jsonStr = await File.ReadAllTextAsync(AppListFile);
            apps = JsonSerializer.Deserialize<FullList>(jsonStr)?.applist?.apps;
        }
        catch (JsonException e)
        {
            Console.WriteLine($"Failed to parse {AppListFile}: {e.Message}");
        }

        if (apps is null || apps.Count == 0)
        {
            Console.WriteLine($"No steam app found in {AppListFile}, skip steam mapping.");
            return;
        }

        Console.WriteLine($"Loaded {apps.Count} steam apps.");
        var appNames = new Dictionary<int, string>();
        foreach (var app in apps)
            appNames[app.appid] = app.name ?? string.Empty;
        Console.WriteLine("Loading Game Titles...");
        var titles = await connection.Table<TitleModel>().ToListAsync();
        var index = new NameIndex(titles.Select(title => (title.Title!, title.VndbId)));

        // vndb自己标了steam链接的，那些app归它标的那个游戏，不让别的游戏再按名字撞上来
        var links = VndbLinks.LoadSteam("./assets/input/");
        var claimed = links.Values.SelectMany(apps => apps).ToHashSet();

        // vndbId -> 此vndb游戏匹配到的最佳steam游戏
        var matches = new Dictionary<int, SteamMatch>();
        using (var pbar = new ProgressBar(apps.Count, "Start updating vn_mapper.db...", Utils.ProgressBar.Options))
        {
            Console.WriteLine("Start matching steam apps...");
            Parallel.ForEach(apps, () => new MatchState(index),
                (app, _, state) =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(app.name) == false && claimed.Contains(app.appid) == false)
                            foreach (var match in state.Matcher.Match(app.name, SimilarityThreshold))
                                Add(state.Matches, match.Id,
                                    new SteamMatch(app.appid, match.Distance, match.Similarity));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to match appid {app.appid} with error: {e.Message}");
                    }

                    pbar.Tick($"Updating id: {app.appid} {app.name}");
                    return state;
                },
                state =>
                {
                    lock (matches)
                        foreach (var (vndbId, match) in state.Matches)
                            Add(matches, vndbId, match);
                });
        }

        // 有现成链接的直接用，盖掉按名字猜的结果
        var authoritative = 0;
        foreach (var (vndbId, linkedApps) in links)
        {
            // 一个游戏可能有多个appid（不同版本/补丁），优先取还在当前app列表里的那个
            var appId = linkedApps.Find(id => appNames.ContainsKey(id));
            if (appId == 0) appId = linkedApps[0];
            matches[vndbId] = new SteamMatch(appId, 0, 1f);
            authoritative++;
        }

        Console.WriteLine($"Matched {matches.Count} vndb games to steam " +
                          $"({authoritative} of them from vndb's own steam links), updating vn_mapper.db...");
        await connection.RunInTransactionAsync(db =>
        {
            foreach (var (vndbId, match) in matches)
            {
                var map = db.Find<MapModel>(vndbId) ?? new MapModel(vndbId);
                map.SteamId = match.AppId;
                map.SteamDistance = match.Distance;
                map.SteamSimilarity = match.Similarity;
                db.InsertOrReplace(map);
            }

            // 加字段之前就存在的行在新字段上是null，统一成“没匹配上”的默认值，方便直接用sql查
            db.Execute("update map set SteamId = 0, SteamDistance = ?, SteamSimilarity = 0 where SteamId is null",
                int.MaxValue);
        });
    }

    private static void Add(Dictionary<int, SteamMatch> matches, int vndbId, SteamMatch match)
    {
        // 相似度更高的结果更好；相似度相同时取appid较小的，好让多次运行的结果保持一致
        if (matches.TryGetValue(vndbId, out var exist) &&
            (match.Similarity < exist.Similarity ||
             (match.Similarity == exist.Similarity && match.AppId >= exist.AppId))) return;
        matches[vndbId] = match;
    }

    private readonly record struct SteamMatch(int AppId, int Distance, float Similarity);

    /// <summary>
    /// 单个线程的匹配状态
    /// </summary>
    private sealed class MatchState(NameIndex index)
    {
        public readonly NameIndex.Matcher Matcher = index.CreateMatcher();
        public readonly Dictionary<int, SteamMatch> Matches = new();
    }
}

#region STEAM_MODEL

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class SteamApp
{
    public int appid { get; set; }
    public string? name { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class Apps
{
    public List<SteamApp>? apps { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class FullList
{
    public Apps? applist { get; set; }
}

#endregion
