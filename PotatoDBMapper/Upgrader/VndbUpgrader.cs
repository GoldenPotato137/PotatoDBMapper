using System.Collections.Concurrent;
using PotatoDBMapper.Models;
using ShellProgressBar;
using SQLite;

namespace PotatoDBMapper.Upgrader;

/// <summary>
/// 基于VNDB数据库来更新
/// </summary>
public class VndbUpgrader
{
    private List<string> GetHeader(string headerFile)
    {
        var result = new List<string>();
        using var reader = new StreamReader(headerFile);
        var line = reader.ReadLine();
        if (line != null)
            result = line.Split('\t').ToList();
        return result;
    }

    private int GetId(string s, int idIndex)
    {
        return Convert.ToInt32(s.Split('\t')[idIndex][1..]);
    }

    public async Task UpdateMapperDb(SQLiteAsyncConnection connection, string inputPath, string[] args, BgmClient bgmClient)
    {
        var header = GetHeader(inputPath + "vn_titles.header");
        var officialIndex = header.FindIndex(x => x == "official");
        var titleIndex = header.FindIndex(x => x == "title");
        var idIndex = header.FindIndex(x => x == "id");
        var langIndex = header.FindIndex(x => x == "lang");
        var latinIndex = header.FindIndex(x => x == "latin");
        if (officialIndex == -1 || titleIndex == -1 || idIndex == -1) return;

        using var reader = new StreamReader(inputPath + "vn_titles");
        var totalLines = GetLinesCount(inputPath + "vn_titles");

        var updateMap = args.Contains("skip-update-map") == false;
        var updateTitle = args.Contains("skip-update-title") == false;
        var displayDetailedProgress = args.Contains("progress");

        // 先把map表整个读进内存。逐条FindAsync/InsertOrReplaceAsync的话，几万次往返
        // 光是sqlite的事务开销就要跑十几分钟，所以这里只在内存里改，最后一次性写回去
        var maps = new Dictionary<int, MapModel>();
        if (updateMap)
            foreach (var map in await connection.Table<MapModel>().ToListAsync())
                maps.TryAdd(map.VndbId, map);
        var mapsToWrite = new ConcurrentBag<MapModel>();
        var titlesToWrite = new ConcurrentBag<TitleModel>();

        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = new List<Task>();
        List<string> lines = new();
        int currentId = 0, currentLine = 0;
        using var pbar = new ProgressBar(totalLines, "Start updating vn_mapper.db...", Utils.ProgressBar.Options);
        Console.WriteLine("Start updating vn_mapper.db...");
        while (await reader.ReadLineAsync() is { } line)
        {
            currentLine++;
            pbar.Tick($"{currentLine} / {totalLines}");
            if ((GetId(line, idIndex) != currentId || reader.EndOfStream) && currentId != 0) // 处理具有相同id的行
            {
                await semaphore.WaitAsync();
                var lineToProcess = lines;
                lines = new List<string>();
                var id = currentId;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        if (updateMap)
                            UpdateMap();
                        if (updateTitle)
                            UpdateTitle();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to update {id} with error: {e.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    void UpdateMap()
                    {
                        var item = maps.GetValueOrDefault(id) ?? new MapModel(id);
                        var name = string.Empty;
                        foreach (var l in lineToProcess)
                        {
                            var data = l.Split('\t');
                            if (data[officialIndex] != "t") continue;
                            name = data[titleIndex];
                            var result = bgmClient.GetId(data[titleIndex]);
                            if (result.similarity > item.BgmSimilarity)
                            {
                                item.BgmDistance = result.distance;
                                item.BgmSimilarity = result.similarity;
                                item.BgmId = result.id;
                            }
                        }

                        if (displayDetailedProgress)
                            Console.WriteLine(
                                $"{name}, vndbId:{item.VndbId}, bgmId:{item.BgmId}, " +
                                $"distance:{item.BgmDistance}, similarity:{item.BgmSimilarity}");
                        mapsToWrite.Add(item);
                    }

                    void UpdateTitle()
                    {
                        var titleToAdd = new List<TitleModel>();
                        foreach (var l in lineToProcess)
                        {
                            var data = l.Split('\t');
                            if (data[officialIndex] != "t" && data[langIndex].Contains("zh") == false && data[langIndex] != "en") continue;
                            if (titleToAdd.Any(title => title.Title == data[titleIndex])) continue;
                            var item = new TitleModel
                            {
                                VndbId = Convert.ToInt32(data[idIndex][1..]),
                                Title = data[titleIndex]
                            };
                            titleToAdd.Add(item);
                            if (data[langIndex] == "ja" && data[latinIndex] != "\\N")
                            {
                                titleToAdd.Add(new TitleModel
                                {
                                    VndbId = Convert.ToInt32(data[idIndex][1..]),
                                    Title = data[latinIndex]
                                });
                            }
                        }

                        if (titleToAdd.Count == 0) return;
                        foreach (var item in titleToAdd)
                            titlesToWrite.Add(item);

                        if (displayDetailedProgress)
                            Console.WriteLine($"{titleToAdd[0].Title} ,vndb_id:{id}, title:{titleToAdd.Count}");
                    }
                }));
            }

            lines.Add(line);
            currentId = GetId(line, idIndex);
        }

        await Task.WhenAll(tasks);
        await WriteAsync(connection, mapsToWrite, titlesToWrite);
    }

    /// <summary>
    /// 把算好的结果一次性写进数据库
    /// </summary>
    private static async Task WriteAsync(SQLiteAsyncConnection connection,
        ConcurrentBag<MapModel> maps, ConcurrentBag<TitleModel> titles)
    {
        if (maps.IsEmpty == false)
        {
            Console.WriteLine($"Writing {maps.Count} maps...");
            await connection.RunInTransactionAsync(db =>
            {
                foreach (var map in maps.OrderBy(map => map.VndbId))
                    db.InsertOrReplace(map);
            });
        }

        if (titles.IsEmpty) return;
        // 同一个名字可能属于多个vndb游戏，而Title是title表的主键，
        // 这里固定取vndb id最小的那个，免得每次运行的结果都不一样
        var deduplicated = new Dictionary<string, int>();
        foreach (var title in titles)
        {
            if (string.IsNullOrEmpty(title.Title)) continue;
            if (deduplicated.TryGetValue(title.Title, out var exist) && exist <= title.VndbId) continue;
            deduplicated[title.Title] = title.VndbId;
        }

        Console.WriteLine($"Writing {deduplicated.Count} titles...");
        await connection.RunInTransactionAsync(db =>
        {
            foreach (var (title, vndbId) in deduplicated.OrderBy(pair => pair.Value))
                db.InsertOrReplace(new TitleModel { Title = title, VndbId = vndbId });
        });
    }

    private static int GetLinesCount(string path)
    {
        using var reader = new StreamReader(path);
        var count = 0;
        while (reader.ReadLine() is not null) count++;
        return count;
    }
}
