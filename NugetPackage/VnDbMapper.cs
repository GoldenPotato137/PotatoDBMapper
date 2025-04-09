using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PotatoDBMapper.Models;
using SQLite;

namespace NugetPackage;

public class VnDbMapper : IDisposable
{
    private SQLiteAsyncConnection? _db;
    public SQLiteAsyncConnection Db
    {
        get
        {
            if (_db is null) throw new InvalidOperationException("VnDbMapper is not initialized.");
            return _db;
        }
    }
    
    public void Init(string dbFile)
    {
        _db?.CloseAsync();
        if (File.Exists(dbFile) == false) 
            throw new FileNotFoundException("Database file not found.");
        _db = new SQLiteAsyncConnection(dbFile);
    }

    public async Task<MapModel?> TryGetMapAsync(int vndbId) => await Db.FindAsync<MapModel>(vndbId);

    /// <summary>
    /// Try to get a list of maps with the given BGM ID. <br/>
    /// If no map is not found, an empty list will be returned.
    /// </summary>
    public Task<List<MapModel>> TryGetMapsWithBgmId(int bgmId) =>
        Db.Table<MapModel>().Where(map => map.BgmId == bgmId).ToListAsync();

    /// <summary>
    /// Try to get a list of maps with the given game name.
    /// </summary>
    /// <param name="gameName"></param>
    /// <param name="minSimilarity"></param>
    /// <returns></returns>
    public async Task<List<(MapModel model, double similarity)>> TryGetMapsWithName
        (string gameName, double minSimilarity = 0.75)
    {
        List<(TitleModel t, double sim)> titles = (await Db.Table<TitleModel>()
                .Where(t => t.Title != null)
                .ToListAsync())
            .Where(t => t.Title!.Similarity(gameName) >= minSimilarity)
            .Select(t => (t, t.Title!.Similarity(gameName)))
            .GroupBy(t => t.t.VndbId)
            .Select(g => g.OrderByDescending(t => t.Item2).First())
            .ToList();

        List<int> vndbIds = titles.Select(t => t.t.VndbId).ToList();
        Dictionary<int, double> vndbIdToSim = titles.ToDictionary(t => t.t.VndbId, t => t.sim);

        return (await Db.Table<MapModel>().Where(map => vndbIds.Contains(map.VndbId)).ToListAsync())
            .Select(map => (map, vndbIdToSim[map.VndbId])).ToList();
    }

    public void Dispose()
    {
        _db?.CloseAsync();
    }
}