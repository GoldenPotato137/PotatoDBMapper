using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PotatoDBMapper.Models;
using ShellProgressBar;
using SQLite;

namespace PotatoDBMapper.Upgrader;

public class SteamUpgrader(SQLiteAsyncConnection connection)
{
    private const float SimilarityThreshold = 0.3f;
    
    public async Task Upgrade()
    {
        Console.WriteLine("Loading Steam Game Lists...");
        string jsonStr = await File.ReadAllTextAsync("./assets/input/steam.json");
        var fullList = JsonSerializer.Deserialize<FullList>(jsonStr);
        Console.WriteLine("Loading Game Titles...");
        var titles = (await connection.Table<TitleModel>().ToListAsync())
            .Where(model => !string.IsNullOrWhiteSpace(model.Title)).ToList();

        var progressBar = new ProgressBar(fullList!.applist.apps.Count, "Start updating vn_mapper.db...",
            Utils.ProgressBar.Options);

        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        List<Task> tasks = [];
        foreach (var appEnum in fullList.applist.apps)
        {
            tasks.Add(Task.Run(async () =>
            {
                var app = appEnum;
                await semaphore.WaitAsync();
                progressBar.Tick($"Updating id: {app.appid} {app.name}" );
                try
                {
                    float maxSimilarity = -1;
                    var dis = 114514;
                    TitleModel? target = null;
                    if (string.IsNullOrEmpty(app.name)) return;
                    foreach (var title in titles)
                    {
                        var status = title.Title!.GetSimilarity(app.name);
                        if (status.similarity > maxSimilarity)
                        {
                            maxSimilarity = status.similarity;
                            dis = status.levenshtein;
                            target = title;
                        }
                    }
                    if (target is not null && maxSimilarity > SimilarityThreshold)
                    {
                        MapModel map = await connection.FindAsync<MapModel>(target.VndbId) ?? new MapModel(target.VndbId);
                        map.SteamId = app.appid;
                        map.SteamSimilarity = maxSimilarity;
                        map.SteamDistance = dis;
                        await connection.InsertOrReplaceAsync(map);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }
        
        await Task.WhenAll(tasks);
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
    public List<SteamApp> apps { get; set; } = null!;
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class FullList
{
    public Apps applist { get; set; } = null!;
}

#endregion