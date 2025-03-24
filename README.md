# PotatoDBMapper

这是一个用于将vndb的游戏id映射到其他游戏数据库的工具（目前只映射到了bangumi）。

## 使用方法

本项目每周一晚上会自动运行更行，大概周二凌晨就会跑出结果，可以在[.\assets\db\ ](https://github.com/GoldenPotato137/PotatoDBMapper/tree/main/assets/db)中找到各个结果表。

所有的表均为sqlite3数据库。

如果你希望手动运行，可以按照以下步骤操作：

1. 下载vndb和bangumi的数据库导出，可以手动下载，也可以使用`.\assets\input\`
   里面的脚本来自动下载，具体请参考[.\assets\input\README.md](https://github.com/GoldenPotato137/PotatoDBMapper/tree/main/assets/input)。
2. cd到项目根目录，输入命令`dotnet run`。
3. 耐心等待30分钟左右，具体时间取决于你的电脑性能。运行结果会保存在`.\assets\db\`中。

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
数值越接近1表示两个游戏名越相似。一般来说这个数值大于0.8就可以认为结果可信了。

表名： title

字段：

- **VndbId**：（int）此字段存储游戏的vndb id。
- **Title**：（string，主键）此字段存储游戏的vndb名字。
