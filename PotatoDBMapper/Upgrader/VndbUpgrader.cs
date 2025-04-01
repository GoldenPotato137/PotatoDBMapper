using System.Diagnostics;
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
        if (officialIndex == -1 || titleIndex == -1 || idIndex == -1) return;
        
        using var reader = new StreamReader(inputPath + "vn_titles");
        var totalLines = GetLinesCount(inputPath + "vn_titles");

        var updateMap = args.Contains("skip-update-map") == false;
        var updateTitle = args.Contains("skip-update-title") == false;
        var displayDetailedProgress = args.Contains("progress");

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
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (updateMap)
                            await UpdateMap();
                        if (updateTitle)
                            await UpdateTitle();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to update {id} with error: {e.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    async Task UpdateMap()
                    {
                        var item = await connection.FindAsync<MapModel>(id) ?? new MapModel(id);
                        var name = string.Empty;
                        foreach (var l in lineToProcess)
                        {
                            var data = l.Split('\t');
                            if (data[officialIndex] != "t") continue;
                            name = data[titleIndex];
                            data[idIndex] = data[idIndex].Replace("v", "");

                            var result = await bgmClient.GetId(data[titleIndex]);
                            if (result.percent > item.BgmSimilarity)
                            {
                                item.BgmDistance = result.Item2;
                                item.BgmSimilarity = result.percent;
                                item.BgmId = result.Item1;
                            }
                        }

                        if (displayDetailedProgress)
                            Console.WriteLine(
                                $"{name}, vndbId:{item.VndbId}, bgmId:{item.BgmId}, " +
                                $"distance:{item.BgmDistance}, similarity:{item.BgmSimilarity}");
                        await connection.InsertOrReplaceAsync(item);
                    }

                    async Task UpdateTitle()
                    {
                        var titleToAdd = new List<TitleModel>();
                        foreach (var l in lineToProcess)
                        {
                            var data = l.Split('\t');
                            if (data[officialIndex] != "t" && data[langIndex].Contains("zh") == false) continue;
                            if (titleToAdd.Any(title => title.Title == data[titleIndex])) continue;
                            var item = new TitleModel
                            {
                                VndbId = Convert.ToInt32(data[idIndex][1..]),
                                Title = data[titleIndex]
                            };
                            titleToAdd.Add(item);
                        }

                        if (titleToAdd.Count == 0) return;
                        foreach (var item in titleToAdd)
                            await connection.InsertOrReplaceAsync(item);

                        if (displayDetailedProgress)
                            Console.WriteLine($"{titleToAdd[0].Title} ,vndb_id:{id}, title:{titleToAdd.Count}");
                    }
                }));
            }

            lines.Add(line);
            currentId = GetId(line, idIndex);
        }

        await Task.WhenAll(tasks);
    }
    
    private static int GetLinesCount(string path)
    {
        using var reader = new StreamReader(path);
        var count = 0;
        while (reader.ReadLine() is not null) count++;
        return count;
    }
}