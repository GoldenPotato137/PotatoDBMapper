# PotatoVnDB Mapper

这是一个用于将vndb的游戏id映射到其他游戏数据库的工具（目前只映射到了bangumi）。

本nuget包为[本项目](https://github.com/GoldenPotato137/PotatoDBMapper)的简单封装，提供了一个简单的接口来获取映射结果。

## 使用方法

使用前请安装本nuget包

```csharp
VnDbMapper mapper = new VnDbMapper();
//可选：不使用捆绑的数据库，自己指定数据库路径 VnDbMapper("your_db_path")
mapper.Init(); //初始化数据库链接

// MapModel为原表<map>中条目的映射，请参考原项目获取这些数据的含义
MapModel map = mapper.GetMap(1); //获取vndb id为1的映射结果

//获取bangumi id为114514的映射结果（一个vndb游戏可能会映射到多个bgm中，故结果是个列表）
List<MapModel> maps = mapper.TryGetMapWithBgmId(114514); 

//搜索某游戏的映射表，返回List<(MapModel, double)>，double为相似度
//async Task<List<(MapModel model, double similarity)>> TryGetMapsWithName(string gameName, double minSimilarity = 0.75)
List<(MapModel, double)> maps = mapper.TryGetMapWithName("近月少女的礼仪", 0.8);

mapper.Dispose(); //释放资源
```