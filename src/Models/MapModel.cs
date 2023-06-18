using SQLite;

namespace PotatoDBMapper.Models;

[Table("map")]
public class MapModel
{
    [PrimaryKey]
    public string? VndbId
    {
        get;
        set;
    }

    public string? BgmId
    {
        get;
        set;
    }
    
    public double BgmSimilarity
    {
        get;
        set;
    }

    public MapModel(string vndbId)
    {
        VndbId = vndbId;
    }

    public MapModel()
    {
        
    }
}