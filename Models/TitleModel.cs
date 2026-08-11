using SQLite;

namespace PotatoDBMapper.Models;

[Table("title")]
public class TitleModel
{
    public int VndbId
    {
        get;
        set;
    }
    
    [PrimaryKey]
    public string? Title
    {
        get;
        set;
    }
}