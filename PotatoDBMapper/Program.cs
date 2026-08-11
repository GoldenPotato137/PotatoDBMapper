using PotatoDBMapper;
using PotatoDBMapper.Models;
using PotatoDBMapper.Upgrader;
using SQLite;

const string inputPath = "./assets/input/";
const string dbPath = "./assets/db/vn_mapper.db";

SQLiteAsyncConnection GetConnection(string path)
{
    // 库文件不存在时sqlite会自己建，只要目录在就行
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    return new SQLiteAsyncConnection(path);
}

var connection = GetConnection(dbPath);
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

if (!args.Contains("no-ymgal"))
{
    var ymgal = new YmgalUpgrader(connection, args);
    await ymgal.Upgrade();
}

if (!args.Contains("no-steam"))
{
    var steam = new SteamUpgrader(connection);
    await steam.Upgrade();
}

await connection.CloseAsync();
return 0;