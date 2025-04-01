using PotatoDBMapper;
using PotatoDBMapper.Models;
using PotatoDBMapper.Upgrader;
using SQLite;

const string inputPath = "./assets/input/";

SQLiteAsyncConnection GetConnection(string path)
{
    if (File.Exists(path) == false)
        File.Create(path);
    var connection = new SQLiteAsyncConnection(path);
    return connection;
}

var connection = GetConnection("./assets/db/vn_mapper.db");
await connection.CreateTableAsync<MapModel>();
await connection.CreateTableAsync<TitleModel>();

if (!args.Contains("no-bgm"))
{
    var bgmClient = new BgmClient();
    var vndb = new VndbUpgrader();
    var bgm = new BgmUpgrader(bgmClient, args);
    await vndb.UpdateMapperDb(connection, inputPath, args, bgmClient);
    await bgm.UpgradeDb(connection);
}

if (!args.Contains("no-steam"))
{
    var steam = new SteamUpgrader(connection);
    await steam.Upgrade();
}

await connection.CloseAsync();
return 0;