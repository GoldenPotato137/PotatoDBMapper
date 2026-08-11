using PotatoDBMapper.Models;
using SQLite;

namespace PotatoDBMapper.Upgrader;

public class BgmUpgrader
{
    private readonly BgmClient _bgmClient;
    private readonly bool _updateTitle;

    public BgmUpgrader(BgmClient client,string[] args)
    {
        _bgmClient = client;
        _updateTitle = args.Contains("skip-update-title") == false;
    }

    public async Task UpgradeDb(SQLiteAsyncConnection db)
    {
        if (_updateTitle == false) return;

        // 把精确匹配上的映射按bgm id索引起来。逐个游戏去where一次的话，
        // 每次都是一遍全表扫描，几万个游戏就是几万遍，实在划不来
        var maps = new Dictionary<int, int>();
        foreach (var map in (await db.Table<MapModel>().Where(map => map.BgmDistance == 0).ToListAsync())
                 .OrderBy(map => map.VndbId))
            maps.TryAdd(map.BgmId, map.VndbId); // 一个bgm游戏可能对应多个vndb游戏，取id最小的那个

        var titles = new Dictionary<string, int>();
        foreach (var game in _bgmClient.GetGames)
        {
            if (string.IsNullOrEmpty(game.NameCn)) continue;
            if (maps.TryGetValue(game.Id, out var vndbId) == false) continue;
            titles[game.NameCn] = vndbId;
            Console.WriteLine($"Add Title, bgm_id:{game.Id}, vndb_id:{vndbId}, title:{game.NameCn}");
        }

        if (titles.Count == 0) return;
        await db.RunInTransactionAsync(connection =>
        {
            foreach (var (title, vndbId) in titles)
                connection.InsertOrReplace(new TitleModel { Title = title, VndbId = vndbId });
        });
    }
}
