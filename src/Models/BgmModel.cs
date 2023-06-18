using SQLite;

namespace PotatoDBMapper.Models;

[Table("bangumi")]
public class BgmModel
{
    [PrimaryKey, AutoIncrement]
    public int Id
    {
        get;
        set;
    }
    
    public string? Name
    {
        get;
        set;
    }
    
    public string? NameCn
    {
        get;
        set;
    }
    
    public bool Nsfw
    {
        get;
        set;
    }
    
    public string? Image
    {
        get;
        set;
    }
    
    public string? ReleaseDate
    {
        get;
        set;
    }

    public string? Summary
    {
        get;
        set;
    }
}