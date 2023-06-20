using System.Diagnostics.CodeAnalysis;
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

    public static bool operator ==(TitleModel x, TitleModel y)
    {
        return x.VndbId == y.VndbId && x.Title == y.Title;
    }

    public static bool operator !=(TitleModel x, TitleModel y)
    {
        return !(x == y);
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is TitleModel titleModel)
            return this == titleModel;
        return false;
    }
    
    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public override int GetHashCode()
    {
        return HashCode.Combine(VndbId, Title);
    }
}