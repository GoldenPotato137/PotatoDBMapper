using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PotatoDBMapper.Models;
using SQLite;

namespace NugetPackage;

/// <summary>
/// 查询映射库（vn_mapper.db）：按vndb id、bangumi id、steam appid、月幕Galgame gid或游戏名，
/// 查出对应的<see cref="MapModel"/>。<br/>
/// 用法是先<see cref="Init"/>再查，用完<see cref="Dispose"/>。数据库可以从项目的db-latest这个release下载
/// </summary>
public class VnDbMapper : IDisposable
{
    private SQLiteAsyncConnection? _db;

    /// <summary>
    /// 底层的数据库连接，需要自己写查询时可以用。<see cref="Init"/>之前访问会抛<see cref="InvalidOperationException"/>
    /// </summary>
    public SQLiteAsyncConnection Db
    {
        get
        {
            if (_db is null) throw new InvalidOperationException("VnDbMapper is not initialized.");
            return _db;
        }
    }

    /// <summary>title表的索引，第一次<see cref="TryGetMapsWithNameCache"/>时才建，见<see cref="TitleIndex"/></summary>
    private Task<TitleIndex>? _titles;

    private readonly object _titlesLock = new object();

    /// <summary>
    /// 打开数据库。重复调用会先关掉上一个连接
    /// </summary>
    /// <param name="dbFile">vn_mapper.db的路径</param>
    /// <exception cref="FileNotFoundException">文件不存在</exception>
    public void Init(string dbFile)
    {
        // 先确认新库在，再动现有的连接，不然这次调用失败了连原来那个库也用不了了
        if (File.Exists(dbFile) == false)
            throw new FileNotFoundException("Database file not found.");
        _db?.CloseAsync();
        lock (_titlesLock) _titles = null; // 换库了，之前的名字索引不能再用
        _db = new SQLiteAsyncConnection(dbFile);
    }

    /// <summary>
    /// 按vndb id查映射，没有则返回null
    /// </summary>
    public async Task<MapModel?> TryGetMapAsync(int vndbId) => await Db.FindAsync<MapModel>(vndbId);

    /// <summary>
    /// Try to get a list of maps with the given BGM ID. <br/>
    /// If no map is not found, an empty list will be returned.
    /// </summary>
    /// <remarks>一个bangumi条目可能对应多个vndb游戏，所以结果是个列表</remarks>
    public Task<List<MapModel>> TryGetMapsWithBgmId(int bgmId) =>
        // id为0表示没匹配到，不是一个能查的id，真拿它去查会把满库没匹配上的行都捞回来
        bgmId <= 0
            ? Task.FromResult(new List<MapModel>())
            : Db.Table<MapModel>().Where(map => map.BgmId == bgmId).ToListAsync();

    /// <summary>
    /// 按steam appid查映射，没有则返回空列表。<br/>
    /// 一个steam游戏可能对应多个vndb游戏（比如全年龄版与18禁版分开收录），所以结果是个列表
    /// </summary>
    public Task<List<MapModel>> TryGetMapsWithSteamId(int steamId) =>
        steamId <= 0
            ? Task.FromResult(new List<MapModel>())
            : Db.Table<MapModel>().Where(map => map.SteamId == steamId).ToListAsync();

    /// <summary>
    /// 按月幕Galgame的游戏档案id（gid）查映射，没有则返回空列表。<br/>
    /// 一个月幕游戏可能对应多个vndb游戏，所以结果是个列表
    /// </summary>
    public Task<List<MapModel>> TryGetMapsWithYmgalId(int ymgalId) =>
        ymgalId <= 0
            ? Task.FromResult(new List<MapModel>())
            : Db.Table<MapModel>().Where(map => map.YmgalId == ymgalId).ToListAsync();

    /// <summary>
    /// Try to get a list of maps with the given game name.
    /// </summary>
    /// <remarks>
    /// 名字会先归一化（全角转半角、统一大小写、去掉空白）再比，规则与建库时一致，见<see cref="Helper.NormalizeName"/>。<br/>
    /// 每次调用都重新扫一遍title表，查一次约一百毫秒，不留下任何缓存。
    /// title表是一行一行读出来比的，读完就扔，所以整张表不会同时堆在内存里。<br/>
    /// 要反复搜索（比如搜索框）的话用<see cref="TryGetMapsWithNameCache"/>，它拿约23MB内存换每次几毫秒
    /// </remarks>
    /// <param name="gameName">游戏名，原名、英文名、中文名都行</param>
    /// <param name="minSimilarity">相似度低于此值的结果会被丢掉，取值[0,1]</param>
    /// <returns>匹配到的映射与对应的相似度，按相似度从高到低排序</returns>
    public async Task<List<(MapModel model, double similarity)>> TryGetMapsWithName
        (string gameName, double minSimilarity = 0.75)
    {
        var db = Db;
        // 扫表是同步且吃cpu的，扔到线程池上去，别把调用方的线程占住
        var hits = await Task.Run(() =>
        {
            var matcher = new NameMatcher(gameName, minSimilarity);
            if (matcher.QueryIsEmpty) return matcher.Result();

            var connection = db.GetConnection();
            using (connection.Lock())
                // Table<T>()是延迟执行的，一行一行往外吐，不会把整张表都读进来
                foreach (var title in connection.Table<TitleModel>())
                    if (string.IsNullOrWhiteSpace(title.Title) == false)
                        matcher.Add(title.Title!.NormalizeName(), title.VndbId);

            return matcher.Result();
        }).ConfigureAwait(false);

        return await LoadMapsAsync(hits).ConfigureAwait(false);
    }

    /// <summary>
    /// 与<see cref="TryGetMapsWithName"/>一样按名字查映射，但会把title表读进内存建索引，之后每次搜索只要几毫秒。<br/>
    /// 第一次调用建索引约一百毫秒，建好后一直留着，十几万个名字约占23MB
    /// </summary>
    /// <remarks>
    /// 介意首次那一下耗时的话，可以提前调<see cref="PreloadTitlesAsync"/>预热。<br/>
    /// 结果与<see cref="TryGetMapsWithName"/>完全一致，只是快慢与内存占用的取舍不同：
    /// 要反复搜索（比如搜索框）就用这个，只查一两次就用那个
    /// </remarks>
    /// <param name="gameName">游戏名，原名、英文名、中文名都行</param>
    /// <param name="minSimilarity">相似度低于此值的结果会被丢掉，取值[0,1]</param>
    /// <returns>匹配到的映射与对应的相似度，按相似度从高到低排序</returns>
    public async Task<List<(MapModel model, double similarity)>> TryGetMapsWithNameCache
        (string gameName, double minSimilarity = 0.75)
    {
        var index = await GetTitlesAsync().ConfigureAwait(false);
        return await LoadMapsAsync(index.Search(gameName, minSimilarity)).ConfigureAwait(false);
    }

    /// <summary>
    /// 提前把title表的索引建好，这样第一次<see cref="TryGetMapsWithNameCache"/>不用等。<br/>
    /// 不调也行，只是首次搜索会慢一点
    /// </summary>
    public Task PreloadTitlesAsync() => GetTitlesAsync();

    /// <summary>
    /// 把按名字匹配到的vndb id查成完整的映射
    /// </summary>
    private async Task<List<(MapModel model, double similarity)>> LoadMapsAsync
        (List<(int vndbId, double similarity)> hits)
    {
        if (hits.Count == 0) return new List<(MapModel, double)>();

        var similarities = hits.ToDictionary(hit => hit.vndbId, hit => hit.similarity);
        var maps = new List<(MapModel model, double similarity)>();
        // 相似度低的时候命中的游戏可能很多，一次in进去太多值会撑爆sql，所以分批查
        foreach (var batch in Batch(hits.Select(hit => hit.vndbId), 500))
        {
            var found = await Db.Table<MapModel>().Where(map => batch.Contains(map.VndbId))
                .ToListAsync().ConfigureAwait(false);
            maps.AddRange(found.Select(map => (map, similarities[map.VndbId])));
        }

        return maps.OrderByDescending(map => map.similarity).ToList();
    }

    private async Task<TitleIndex> GetTitlesAsync()
    {
        Task<TitleIndex> loading;
        lock (_titlesLock)
            loading = _titles ??= LoadTitlesAsync(Db);

        try
        {
            return await loading.ConfigureAwait(false);
        }
        catch
        {
            // 读失败了就把缓存清掉，让下次调用重来一遍，别把这个失败的结果一直留着
            lock (_titlesLock)
                if (_titles == loading)
                    _titles = null;
            throw;
        }
    }

    private static async Task<TitleIndex> LoadTitlesAsync(SQLiteAsyncConnection db) =>
        new TitleIndex(await db.Table<TitleModel>().ToListAsync().ConfigureAwait(false));

    private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
    {
        var batch = new List<T>(size);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count < size) continue;
            yield return batch;
            batch = new List<T>(size);
        }

        if (batch.Count > 0) yield return batch;
    }

    /// <summary>
    /// 关掉数据库连接，并释放title表的索引
    /// </summary>
    public void Dispose()
    {
        _db?.CloseAsync();
        _db = null;
        lock (_titlesLock) _titles = null;
    }
}
