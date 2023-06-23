using PotatoDBMapper;
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
var bgmClient = new BgmClient();
var vndb = new VndbUpgrader();
var bgm = new BgmUpgrader(bgmClient, args);

// await vndb.UpdateMapperDb(connection, inputPath, args, bgmClient);
await bgm.UpgradeDb(connection);

await connection.CloseAsync();
return 0;