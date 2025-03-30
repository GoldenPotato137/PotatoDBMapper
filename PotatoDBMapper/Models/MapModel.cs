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

    public MapModel(int vndbId)
    {
        VndbId = vndbId;
    }

    public MapModel()
    {
    }
}