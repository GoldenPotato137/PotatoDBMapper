using SQLite;

namespace PotatoDBMapper.Models;

[Table("map")]
public class MapModel
{
    [PrimaryKey, AutoIncrement]
    public int VndbId
    {
        get;
        set;
    }

    public int BgmId
    {
        get;
        set;
    }

    public int BgmDistance
    {
        get;
        set;
    } = int.MaxValue;

    public float BgmSimilarity { get; set; } = 0f;
    
    public int SteamId { get; set; }
    
    public int SteamDistance { get; set; } = int.MaxValue;
    
    public float SteamSimilarity { get; set; } = 0f;

    /// <summary>
    /// 月幕Galgame（ymgal）的游戏档案id（gid）
    /// </summary>
    public int YmgalId { get; set; }

    public int YmgalDistance { get; set; } = int.MaxValue;

    public float YmgalSimilarity { get; set; } = 0f;

    public MapModel(int vndbId)
    {
        VndbId = vndbId;
    }

    public MapModel()
    {
    }
}