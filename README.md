# PotatoDBMapper

这是一个用于将vndb的游戏id映射到其他游戏数据库的工具（目前映射到了bangumi、steam和月幕Galgame）。

## 使用方法

本项目每周一晚上会自动运行更新，大概周二凌晨就会跑出结果，覆盖发布在
[db-latest](https://github.com/GoldenPotato137/PotatoDBMapper/releases/tag/db-latest)这一个release里，地址固定：

```
https://github.com/GoldenPotato137/PotatoDBMapper/releases/download/db-latest/vn_mapper.db
```

结果为sqlite3数据库。

如果你希望手动运行，可以按照以下步骤操作：

1. 下载vndb、bangumi、steam和月幕Galgame的数据库导出，可以手动下载，也可以使用`.\assets\input\`
   里面的脚本来自动下载，具体请参考[.\assets\input\README.md](https://github.com/GoldenPotato137/PotatoDBMapper/tree/main/assets/input)。
2. cd到项目根目录，输入命令`dotnet run --project .\PotatoDBMapper\PotatoDBMapper.csproj`。
3. 等一分钟左右，具体时间取决于你的电脑性能（大头是读那几个G的输入文件）。运行结果会保存在`.\assets\db\`中。
4. 如果你希望在IDE中编译运行，不要忘了修改工作目录到根目录~ （防止找不到asset里的数据库）

只想跑其中一部分的话，可以在命令后面加这些参数：

- `no-bgm`：跳过vndb导出的处理与bangumi的匹配
- `no-ymgal`：跳过月幕Galgame的匹配
- `no-steam`：跳过steam的匹配
- `skip-update-map` / `skip-update-title`：不更新map表 / 不更新title表
- `progress`：打印每条记录的详细进度

月幕Galgame与steam的输入文件不存在时，对应的匹配会自动跳过，不影响其他部分。

## 表结构

### vn_mapper.db

表名： map

字段：

- **VndbId**：（int，主键，自增）此字段存储游戏的vndb id。
- **BgmId**：（int）此字段存储对应的bangumi的id。
- **BgmDistance**
  ：（int）此字段存储从vndb的游戏名到bangumi的游戏名的修改距离。它表示两个游戏名之间的相似度，数值越小，表示两个游戏名越相似。一般来说这个数值>
  3就可以认为结果不可信了。
- **BgmSimilarity**: (float) 此字段存储从vndb的游戏名到bangumi的游戏名的相似度。`Similarity = 1 - Distance / max(Length(vndb), Length(bgm))`。
数值越接近1表示两个游戏名越相似。一般来说这个数值大于0.8就可以认为结果可信了。相似度低于0.7的结果不会被写入。
- **SteamId**：（int）此字段存储对应的steam的appid，没有匹配到时为0。
- **SteamDistance**：（int）此字段存储从vndb的游戏名到steam的游戏名的修改距离，含义同**BgmDistance**。
- **SteamSimilarity**：（float）此字段存储从vndb的游戏名到steam的游戏名的相似度。
`Similarity = 1 - Distance / max(Length(vndb), Length(steam))`，相似度低于0.7的结果不会被写入。
- **YmgalId**：（int）此字段存储对应的[月幕Galgame](https://www.ymgal.games)的游戏档案id（gid），没有匹配到时为0。
  对应的页面为`https://www.ymgal.games/ga{YmgalId}`。
- **YmgalDistance**：（int）此字段存储从vndb的游戏名到月幕Galgame的游戏名的修改距离，含义同**BgmDistance**。
  这里的游戏名包含月幕的原名、中文名与别名，取其中最相似的一个来计算。
- **YmgalSimilarity**：（float）此字段存储从vndb的游戏名到月幕Galgame的游戏名的相似度。
`Similarity = 1 - Distance / max(Length(vndb), Length(ymgal))`，相似度低于0.7的结果不会被写入。
数值越接近1表示两个游戏名越相似，一般来说这个数值大于0.8就可以认为结果可信了。

表名： title

字段：

- **VndbId**：（int）此字段存储游戏的vndb id。
- **Title**：（string，主键）此字段存储游戏的名字。除了vndb的名字，还包含了从bangumi与月幕Galgame补充过来的中文名。

## 数据来源

- [vndb](https://vndb.org)：[数据库导出](https://dl.vndb.org/dump/)
- [Bangumi 番组计划](https://bgm.tv)：[Bangumi Archive](https://github.com/bangumi/Archive)
- [Steam](https://store.steampowered.com)：[IStoreService/GetAppList](https://partner.steamgames.com/doc/webapi/IStoreService#GetAppList)（需要自己申请web api key，
  具体见[.\assets\input\README.md](https://github.com/GoldenPotato137/PotatoDBMapper/tree/main/assets/input)）
- [月幕Galgame](https://www.ymgal.games)：[data-export](https://github.com/ymgal/data-export)

其中月幕Galgame的数据仅限非商业用途使用，具体请参考其[使用者须知](https://github.com/ymgal/data-export#使用者须知)。
