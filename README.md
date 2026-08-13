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

## 在C#里查映射

不想自己写sql的话，可以直接用[PotatoVnDBMapper](https://www.nuget.org/packages/PotatoVnDBMapper)这个nuget包，
它封装了按vndb id、bangumi id、steam appid、月幕Galgame gid以及游戏名查映射的方法，用法见
[NugetPackage/README.md](https://github.com/GoldenPotato137/PotatoDBMapper/blob/main/NugetPackage/README.md)。

只需要表结构、想自己写查询的话，可以只装
[PotatoVnDBMapper.Models](https://www.nuget.org/packages/PotatoVnDBMapper.Models)。
两个包版本号一致，一起发布。

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
数值越接近1表示两个游戏名越相似。
- **SteamId**：（int）此字段存储对应的steam的appid，没有匹配到时为0。
- **SteamDistance**：（int）此字段存储从vndb的游戏名到steam的游戏名的修改距离，含义同**BgmDistance**。
- **SteamSimilarity**：（float）此字段存储从vndb的游戏名到steam的游戏名的相似度。
`Similarity = 1 - Distance / max(Length(vndb), Length(steam))`，含义同**BgmSimilarity**。
- **YmgalId**：（int）此字段存储对应的[月幕Galgame](https://www.ymgal.games)的游戏档案id（gid），没有匹配到时为0。
  对应的页面为`https://www.ymgal.games/ga{YmgalId}`。
- **YmgalDistance**：（int）此字段存储从vndb的游戏名到月幕Galgame的游戏名的修改距离，含义同**BgmDistance**。
  这里的游戏名包含月幕的原名、中文名与别名，取其中最相似的一个来计算。
- **YmgalSimilarity**：（float）此字段存储从vndb的游戏名到月幕Galgame的游戏名的相似度。
`Similarity = 1 - Distance / max(Length(vndb), Length(ymgal))`，含义同**BgmSimilarity**。

表名： title

字段：

- **VndbId**：（int）此字段存储游戏的vndb id。
- **Title**：（string，主键）此字段存储游戏的名字。除了vndb的名字，还包含了从bangumi与月幕Galgame补充过来的中文名。

## 正确率与召回率

实验方式：抽取1890条映射逐条核对，判断两边是不是同一部作品。由于低相似度的映射没几条， 
按比例抽根本抽不到，所以对它们做了超采样（每个相似度区间抽80~220条），最后再按各区间在全库的
实际条数加权，还原成全库的估计值。

正确率 = 判为同一部作品的条数 / 已判定的条数，判不准的存疑条目不计入分母：

| 相似度区间     | bangumi条数 | bangumi正确率 | steam条数 | steam正确率 | 月幕条数 | 月幕正确率 |
|:--------------|-----------:|-------------:|---------:|-----------:|--------:|----------:|
| <0.70         | 339        | 78.6%        | 98       | 81.0%      | 81      | 70.8%     |
| 0.70~0.80     | 390        | 34.3%        | 123      | 32.6%      | 94      | 38.2%     |
| 0.80~0.90     | 491        | 57.8%        | 155      | 55.2%      | 130     | 75.7%     |
| 0.90~1.00     | 435        | 80.0%        | 204      | 59.1%      | 243     | 94.1%     |
| =1.00         | 28269      | 99.5%        | 10137    | 98.8%      | 30758   | ≈100%     |

加权到全库后，各阈值的表现如下。其中：

- **正确率**：相似度≥阈值的映射里，两边确实是同一部作品的比例
- **召回率**：库里全部正确映射中，因相似度≥阈值而被保留下来的比例，衡量卡阈值会误伤多少对的结果
  （只针对已经匹配出来的映射，不包括没匹配上的游戏）

| Similarity阈值 | bangumi正确率 | bangumi召回率 | steam正确率 | steam召回率 | 月幕正确率 | 月幕召回率 |
|:---------------|-------------:|-------------:|-----------:|-----------:|----------:|----------:|
| 不过滤          | 97.5%        | 100%         | 96.5%      | 100%       | 99.6%     | 100%      |
| ≥0.70          | 97.7%        | 99.1%        | 96.6%      | 99.2%      | 99.7%     | 99.8%     |
| ≥0.80          | 98.6%        | 98.6%        | 97.4%      | 98.8%      | 99.9%     | 99.7%     |
| ≥0.90          | 99.2%        | 97.7%        | 98.0%      | 98.0%      | ≈100%     | 99.4%     |
| =1.00          | 99.5%        | 96.5%        | 98.8%      | 96.9%      | ≈100%     | 98.7%     |

## 数据来源

- [vndb](https://vndb.org)：[数据库导出](https://dl.vndb.org/dump/)
- [Bangumi 番组计划](https://bgm.tv)：[Bangumi Archive](https://github.com/bangumi/Archive)
- [Steam](https://store.steampowered.com)：[IStoreService/GetAppList](https://partner.steamgames.com/doc/webapi/IStoreService#GetAppList)（需要自己申请web api key，
  具体见[.\assets\input\README.md](https://github.com/GoldenPotato137/PotatoDBMapper/tree/main/assets/input)）
- [月幕Galgame](https://www.ymgal.games)：[data-export](https://github.com/ymgal/data-export)

其中月幕Galgame的数据仅限非商业用途使用，具体请参考其[使用者须知](https://github.com/ymgal/data-export#使用者须知)。
