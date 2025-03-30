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
        
        var semaphore = new SemaphoreSlim(24);
        var games = _bgmClient.GetGames;
        List<Task> tasks = new();
        foreach (var game in games)
        {
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    if(string.IsNullOrEmpty(game.NameCn)) return;
                    var mapResults = db.Table<MapModel>().Where(g => g.BgmDistance == 0 && g.BgmId == game.Id);
                    if (await mapResults.CountAsync() == 0) return;
                    var mapResult = await mapResults.FirstAsync();
                    var item = new TitleModel
                    {
                        Title = game.NameCn,
                        VndbId = mapResult.VndbId
                    };
                    await db.InsertOrReplaceAsync(item);
                    Console.WriteLine($"Add Title, bgm_id:{game.Id}, vndb_id:{item.VndbId}, title:{item.Title}");
                }
                finally
                {
                    semaphore.Release();
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }
}