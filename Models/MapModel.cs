using SQLite;

namespace PotatoDBMapper.Models;

/// <summary>
/// map表中的一条映射：一个vndb游戏，与它在bangumi、steam、月幕Galgame里对应的游戏。<br/>
/// 各库的Id为0表示没匹配到；Similarity越接近1表示两边的游戏名越像，
/// 各阈值下的正确率与召回率见项目README。
/// </summary>
[Table("map")]
public class MapModel
{
    /// <summary>
    /// 游戏的vndb id，即vndb页面`https://vndb.org/v{VndbId}`中的数字
    /// </summary>
    [PrimaryKey, AutoIncrement]
    public int VndbId
    {
        get;
        set;
    }

    /// <summary>
    /// 对应的[bangumi](https://bgm.tv)条目id，没有匹配到时为0
    /// </summary>
    public int BgmId
    {
        get;
        set;
    }

    /// <summary>
    /// 从vndb的游戏名到bangumi的游戏名的编辑距离，越小表示两个名字越像
    /// </summary>
    public int BgmDistance
    {
        get;
        set;
    } = int.MaxValue;

    /// <summary>
    /// 从vndb的游戏名到bangumi的游戏名的相似度：<c>1 - BgmDistance / max(两个名字的长度)</c>
    /// </summary>
    public float BgmSimilarity { get; set; } = 0f;

    /// <summary>
    /// 对应的steam appid，没有匹配到时为0
    /// </summary>
    public int SteamId { get; set; }

    /// <summary>
    /// 从vndb的游戏名到steam的游戏名的编辑距离，含义同<see cref="BgmDistance"/>
    /// </summary>
    public int SteamDistance { get; set; } = int.MaxValue;

    /// <summary>
    /// 从vndb的游戏名到steam的游戏名的相似度，含义同<see cref="BgmSimilarity"/>
    /// </summary>
    public float SteamSimilarity { get; set; } = 0f;

    /// <summary>
    /// 月幕Galgame（ymgal）的游戏档案id（gid），没有匹配到时为0。
    /// 对应的页面为`https://www.ymgal.games/ga{YmgalId}`
    /// </summary>
    public int YmgalId { get; set; }

    /// <summary>
    /// 从vndb的游戏名到月幕Galgame的游戏名的编辑距离，含义同<see cref="BgmDistance"/>。
    /// 这里的游戏名包含月幕的原名、中文名与别名，取其中最相似的一个来算
    /// </summary>
    public int YmgalDistance { get; set; } = int.MaxValue;

    /// <summary>
    /// 从vndb的游戏名到月幕Galgame的游戏名的相似度，含义同<see cref="BgmSimilarity"/>
    /// </summary>
    public float YmgalSimilarity { get; set; } = 0f;

    /// <summary>
    /// 新建一条只填了vndb id的映射
    /// </summary>
    public MapModel(int vndbId)
    {
        VndbId = vndbId;
    }

    /// <summary>
    /// 空构造，sqlite-net读表时用
    /// </summary>
    public MapModel()
    {
    }
}
