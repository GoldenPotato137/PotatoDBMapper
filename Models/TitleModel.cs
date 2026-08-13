using SQLite;

namespace PotatoDBMapper.Models;

/// <summary>
/// title表中的一条游戏名。一个游戏会有多条（原名、英文名，以及从bangumi与月幕Galgame补过来的中文名），
/// 按名字找游戏时用
/// </summary>
[Table("title")]
public class TitleModel
{
    /// <summary>
    /// 这个名字所属游戏的vndb id
    /// </summary>
    public int VndbId
    {
        get;
        set;
    }

    /// <summary>
    /// 游戏名，全库唯一
    /// </summary>
    [PrimaryKey]
    public string? Title
    {
        get;
        set;
    }
}
