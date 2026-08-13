# PotatoVnDBMapper.Models

[PotatoDBMapper](https://github.com/GoldenPotato137/PotatoDBMapper)映射库（`vn_mapper.db`）的表结构定义，
只有`map`与`title`两张表对应的[sqlite-net](https://github.com/praeclarum/sqlite-net)模型，不含任何查询逻辑。

想直接查映射的话请用[PotatoVnDBMapper](https://www.nuget.org/packages/PotatoVnDBMapper)，它已经引用了本包。
自己拿sqlite读库时才需要单独安装本包。

```csharp
using PotatoDBMapper.Models;
using SQLite;

var db = new SQLiteAsyncConnection("vn_mapper.db");
MapModel? map = await db.FindAsync<MapModel>(10680);
Console.WriteLine($"{map?.BgmId} {map?.SteamId} {map?.YmgalId}");
```

数据库本身可以从[db-latest](https://github.com/GoldenPotato137/PotatoDBMapper/releases/tag/db-latest)下载，
每周一晚上自动更新，地址固定：

```
https://github.com/GoldenPotato137/PotatoDBMapper/releases/download/db-latest/vn_mapper.db
```

各字段的含义见[项目README](https://github.com/GoldenPotato137/PotatoDBMapper#表结构)。
