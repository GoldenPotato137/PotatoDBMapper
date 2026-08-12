namespace PotatoDBMapper;

/// <summary>
/// vndb的导出里自带外部链接表，其中就有steam的appid。这是vndb编辑手工维护的，
/// 比拿名字去猜靠得住得多（同名不同游戏、副标题差异之类的坑全都绕开了）。<br/>
/// 关联路径：vn &lt;- releases_vn &lt;- releases_extlinks -&gt; extlinks(site=steam)
/// </summary>
public static class VndbLinks
{
    /// <summary>
    /// 读出每个vndb游戏对应的steam appid。缺文件时返回空表（退回到按名字匹配）
    /// </summary>
    public static Dictionary<int, List<int>> LoadSteam(string inputPath)
    {
        var result = new Dictionary<int, List<int>>();
        string extlinks = inputPath + "extlinks",
            releaseLinks = inputPath + "releases_extlinks",
            releaseVns = inputPath + "releases_vn";
        if (File.Exists(extlinks) == false || File.Exists(releaseLinks) == false ||
            File.Exists(releaseVns) == false)
        {
            Console.WriteLine("vndb dump中的extlinks/releases_extlinks/releases_vn不全, " +
                              "skip vndb's own steam links.");
            return result;
        }

        // extlinks: id  site  value
        var appOfLink = new Dictionary<string, int>();
        foreach (var data in ReadTsv(extlinks, 3))
            if (data[1] == "steam" && int.TryParse(data[2], out var appId))
                appOfLink[data[0]] = appId;

        // releases_extlinks: id(发行版)  link
        var appOfRelease = new Dictionary<string, List<int>>();
        foreach (var data in ReadTsv(releaseLinks, 2))
        {
            if (appOfLink.TryGetValue(data[1], out var appId) == false) continue;
            if (appOfRelease.TryGetValue(data[0], out var apps)) apps.Add(appId);
            else appOfRelease[data[0]] = [appId];
        }

        // releases_vn: id(发行版)  vid  rtype
        foreach (var data in ReadTsv(releaseVns, 2))
        {
            if (appOfRelease.TryGetValue(data[0], out var apps) == false) continue;
            if (int.TryParse(data[1].AsSpan(1), out var vndbId) == false) continue;
            if (result.TryGetValue(vndbId, out var exist))
            {
                foreach (var appId in apps)
                    if (exist.Contains(appId) == false)
                        exist.Add(appId);
            }
            else result[vndbId] = [..apps];
        }

        foreach (var apps in result.Values)
            apps.Sort();
        Console.WriteLine($"Loaded steam links of {result.Count} vndb games from vndb dump.");
        return result;
    }

    private static IEnumerable<string[]> ReadTsv(string path, int minColumns)
    {
        using var reader = new StreamReader(path);
        while (reader.ReadLine() is { } line)
        {
            var data = line.Split('\t');
            if (data.Length >= minColumns) yield return data;
        }
    }
}
