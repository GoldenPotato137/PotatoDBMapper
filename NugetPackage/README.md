# PotatoVnDB Mapper

这是一个用于将vndb的游戏id映射到其他游戏数据库的工具（目前映射到了bangumi、steam和月幕Galgame）。

本nuget包为[本项目](https://github.com/GoldenPotato137/PotatoDBMapper)的简单封装，提供了一个简单的接口来获取映射结果。

## 使用方法

使用前请安装本nuget包，并下载映射库`vn_mapper.db`。它每周一晚上自动更新，覆盖发布在
[db-latest](https://github.com/GoldenPotato137/PotatoDBMapper/releases/tag/db-latest)这一个release里，地址固定：

```
https://github.com/GoldenPotato137/PotatoDBMapper/releases/download/db-latest/vn_mapper.db
```

```csharp
using NugetPackage;
using PotatoDBMapper.Models;

var mapper = new VnDbMapper();
mapper.Init("path_to_db"); //打开数据库

// MapModel为原表<map>中条目的映射，请参考原项目获取这些数据的含义
MapModel? map = await mapper.TryGetMapAsync(10680); //获取vndb id为10680的映射结果
Console.WriteLine($"{map?.BgmId} {map?.SteamId} {map?.YmgalId}");

//反过来查也行，一个游戏可能会映射到多个vndb游戏，故结果是个列表
List<MapModel> byBgm = await mapper.TryGetMapsWithBgmId(44123); //bangumi条目id
List<MapModel> bySteam = await mapper.TryGetMapsWithSteamId(1776970); //steam appid
List<MapModel> byYmgal = await mapper.TryGetMapsWithYmgalId(31147); //月幕Galgame的gid

//搜索某游戏的映射表，返回List<(MapModel, double)>，double为相似度，按相似度从高到低排序
//async Task<List<(MapModel model, double similarity)>> TryGetMapsWithName(string gameName, double minSimilarity = 0.75)
List<(MapModel, double)> maps = await mapper.TryGetMapsWithName("近月少女的礼仪", 0.8);

//要反复搜索的话用带索引的这个，结果与上面完全一致，但每次只要几毫秒
List<(MapModel, double)> fast = await mapper.TryGetMapsWithNameCache("近月少女的礼仪", 0.8);

mapper.Dispose(); //释放资源
```

按名字搜索时，原名、英文名与中文名都可以，名字会先归一化（全角转半角、统一大小写、去掉空白）
再比较，规则与建库时一致。

### 按名字搜索的两个接口

两者结果完全一致，区别只在时间与内存的取舍：

| 方法                        | 首次          | 之后每次   | 常驻内存  |
|:---------------------------|:-------------|:---------|:--------|
| `TryGetMapsWithName`       | 约100ms       | 约100ms  | 不占      |
| `TryGetMapsWithNameCache`  | 约110ms建索引  | 几毫秒     | 约23MB   |

`TryGetMapsWithName`不建也不用索引，title表一行一行读出来比完就扔，整张表不会同时堆在内存里。
只查一两次、或者不想让那23MB一直占着的话用这个。

`TryGetMapsWithNameCache`第一次调用会把title表读进内存建索引（十几万个名字），之后的搜索都在内存里做。
介意首次那一下耗时的话，可以提前调`PreloadTitlesAsync()`预热。适合搜索框这种要反复查的场景。

## 关于映射结果

`MapModel`里各库的`Id`为0表示没匹配到，`Similarity`是两边游戏名的相似度，越接近1越可信。
各阈值下的正确率与召回率见[项目README](https://github.com/GoldenPotato137/PotatoDBMapper#正确率与召回率)，
一般卡`Similarity >= 0.8`就能有98%以上的正确率。

只需要表结构、想自己写查询的话，可以只装
[PotatoVnDBMapper.Models](https://www.nuget.org/packages/PotatoVnDBMapper.Models)。
