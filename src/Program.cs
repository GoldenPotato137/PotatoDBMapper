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
    while (reader.ReadLine() is { } line)
    {
        var data = line.Split('\t');
        if (data[officialIndex] != "t") continue;
        var item = await connection.FindAsync<MapModel>(data[idIndex]) ?? new MapModel(data[idIndex]);
        if (item.BgmId is not (null or "0")) continue;
        item.BgmId = (await bgmClient.GetId(data[titleIndex])).ToString();
        Console.WriteLine($"{data[titleIndex]}, vndbId:{item.VndbId}, bgmId:{item.BgmId}");
        await connection.InsertOrReplaceAsync(item);
        await Task.Delay(5000); // 减少服务器压力
    }
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