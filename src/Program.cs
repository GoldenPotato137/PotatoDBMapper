using PotatoDBMapper;
using PotatoDBMapper.Models;
using SQLite;

const string inputPath = "./assets/input/";


List<string> GetHeader(string headerFile)
{
    var result = new List<string>();
    using var reader = new StreamReader(headerFile);
    var line = reader.ReadLine();
    if (line != null)
        result = line.Split('\t').ToList();
    return result;
}

int GetId(string s, int idIndex)
{
    return Convert.ToInt32(s.Split('\t')[idIndex][1..]);
}

async Task UpdateMapperDb(SQLiteAsyncConnection connection)
{
    var bgmClient = new BgmClient();
    var header = GetHeader(inputPath + "vn_titles.header");
    var officialIndex = header.FindIndex(x => x == "official");
    var titleIndex = header.FindIndex(x => x == "title");
    var idIndex = header.FindIndex(x => x == "id");
    var langIndex = header.FindIndex(x => x == "lang");
    if (officialIndex == -1 || titleIndex == -1 || idIndex == -1) return;

    await connection.CreateTableAsync<MapModel>();
    await connection.CreateTableAsync<TitleModel>();
    using var reader = new StreamReader(inputPath + "vn_titles");
    Console.WriteLine("Start updating vn_mapper.db...");

    var updateMap = args.Contains("skip-update-map") == false;
    var updateTitle = args.Contains("skip-update-title") == false;

    var semaphore = new SemaphoreSlim(24);
    var tasks = new List<Task>();
    List<string> lines = new();
    var currentId = 0;
    while (reader.ReadLine() is { } line)
    {
        if ((GetId(line, idIndex) != currentId || reader.EndOfStream) && currentId != 0) // 处理具有相同id的行
        {
            var lineToProcess = lines;
            lines = new List<string>();
            var id = currentId;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    if(updateMap)
                        await UpdateMap();
                    if(updateTitle)
                        await UpdateTitle();
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
                        if (result.Item2 < item.BgmDistance)
                        {
                            item.BgmDistance = result.Item2;
                            item.BgmId = result.Item1;
                        }
                    }

                    Console.WriteLine($"{name}, vndbId:{item.VndbId}, bgmId:{item.BgmId}, distance:{item.BgmDistance}");
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
                    if(titleToAdd.Count == 0) return;
                    foreach (var item in titleToAdd)
                        await connection.InsertOrReplaceAsync(item);

                    Console.WriteLine($"{titleToAdd[0].Title} ,vndb_id:{id}, title:{titleToAdd.Count}");
                }
            }));
        }

        lines.Add(line);
        currentId = GetId(line, idIndex);
    }

    await Task.WhenAll(tasks);
}

SQLiteAsyncConnection GetConnection(string path)
{
    if (File.Exists(path) == false)
        File.Create(path);
    var connection = new SQLiteAsyncConnection(path);
    return connection;
}

var connection = GetConnection("./assets/db/vn_mapper.db");

await UpdateMapperDb(connection);

await connection.CloseAsync();
return 0;