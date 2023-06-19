using PotatoDBMapper;
using PotatoDBMapper.Models;
using SQLite;

const string inputPath = "./assets/input/";
const string dbPath = "./assets/db/vn_mapper.db";
var bgmClient = new BgmClient();

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
    var header = GetHeader(inputPath + "vn_titles.header");
    var officialIndex = header.FindIndex(x => x == "official");
    var titleIndex = header.FindIndex(x => x == "title");
    var idIndex = header.FindIndex(x => x == "id");
    if (officialIndex == -1 || titleIndex == -1 || idIndex == -1) return;

    await connection.CreateTableAsync<MapModel>();
    using var reader = new StreamReader(inputPath + "vn_titles");
    Console.WriteLine("Start updating vn_mapper.db...");

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
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        lines.Add(line);
        currentId = GetId(line, idIndex);
    }

    await Task.WhenAll(tasks);
}

SQLiteAsyncConnection GetConnection()
{
    if (File.Exists(dbPath) == false)
        File.Create(dbPath);
    var connection = new SQLiteAsyncConnection(dbPath);
    return connection;
}

var connection = GetConnection();
await UpdateMapperDb(connection);
await connection.CloseAsync();