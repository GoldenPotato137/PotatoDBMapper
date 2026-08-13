using System.Diagnostics;

namespace NugetPackage.Test;

[TestFixture]
public class VnDbMapperTest
{
    private readonly VnDbMapper _mapper = new VnDbMapper();

    [OneTimeSetUp]
    public void Init()
    {
        if (File.Exists("vn_mapper.db") == false)
            Assert.Ignore("找不到vn_mapper.db，可以从 " +
                          "https://github.com/GoldenPotato137/PotatoDBMapper/releases/download/db-latest/vn_mapper.db " +
                          "下载后放到./assets/db/下再跑测试。");
        _mapper.Init("vn_mapper.db");
    }

    [Test]
    [TestCase(10680)] //近月
    [TestCase(15293)] //近月2
    [TestCase(20623)] //近月2.1
    [TestCase(12246)] //少女理论
    public async Task TryGetMapTest(int vndbId)
    {
        var result = await _mapper.TryGetMapAsync(vndbId);
        Assert.That(result != null);
    }

    [Test]
    [TestCase(44123)]
    [TestCase(406121)]
    public async Task TryGetMapWithBgmId(int bgmId)
    {
        // 一个bgm游戏可能对应多个vndb游戏，具体几个会随上游数据变，所以这里只检查结果本身对不对
        var result = await _mapper.TryGetMapsWithBgmId(bgmId);
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.All(map => map.BgmId == bgmId));
    }

    [Test]
    [TestCase(1776970)] //近月少女的礼仪
    [TestCase(1144400)] //千恋＊万花
    public async Task TryGetMapWithSteamId(int steamId)
    {
        var result = await _mapper.TryGetMapsWithSteamId(steamId);
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.All(map => map.SteamId == steamId));
    }

    [Test]
    [TestCase(31147)] //近月少女的礼仪
    [TestCase(22374)] //千恋＊万花
    public async Task TryGetMapWithYmgalId(int ymgalId)
    {
        var result = await _mapper.TryGetMapsWithYmgalId(ymgalId);
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.All(map => map.YmgalId == ymgalId));
    }

    // 各库的id为0是"没匹配到"的意思，拿它去查会把满库没匹配上的行都捞回来，得挡住
    [Test]
    public async Task TryGetMapWithEmptyIdTest()
    {
        Assert.That(await _mapper.TryGetMapsWithBgmId(0), Is.Empty);
        Assert.That(await _mapper.TryGetMapsWithSteamId(0), Is.Empty);
        Assert.That(await _mapper.TryGetMapsWithYmgalId(0), Is.Empty);
    }

    [Test]
    [TestCase("近月少女的礼仪", 10680)]
    [TestCase("大图书馆的牧羊人 -Dreaming Sheep-", 12480)]
    public async Task TryGetMapWithName(string name, int targetVndbId)
    {
        var result = await _mapper.TryGetMapsWithName(name);
        Assert.That(result.Any(map => map.model.VndbId == targetVndbId));
    }

    // 归一化之后再比，所以全角半角、大小写、空格的写法不同也应该能查到同一个游戏
    [Test]
    [TestCase("千恋＊万花")]
    [TestCase("千恋*万花")]
    [TestCase("SENREN＊BANKA")]
    [TestCase("senren * banka")]
    public async Task TryGetMapWithNameNormalizedTest(string name)
    {
        var result = await _mapper.TryGetMapsWithName(name);
        Assert.That(result.Any(map => map.model.VndbId == 19073), $"{name} 没查到千恋万花");
    }

    // 这里故意少写一个"的"，好让它走模糊匹配那条路，别一上来就精确命中
    [Test]
    public async Task TryGetMapWithNameSortedTest()
    {
        var result = await _mapper.TryGetMapsWithName("近月少女礼仪", 0.5);
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.Select(map => map.similarity), Is.Ordered.Descending);
        Assert.That(result.All(map => map.similarity >= 0.5));
        // 同一个游戏只该出现一次，不然多个名字命中同一个游戏时结果里会有重复
        Assert.That(result.Select(map => map.model.VndbId).Distinct().Count(), Is.EqualTo(result.Count));
    }

    // 走索引的那个接口，结果必须和直接扫库的完全一样，只是快慢与内存占用的取舍不同
    [Test]
    [TestCase("近月少女的礼仪", 0.75)] //精确命中
    [TestCase("近月少女礼仪", 0.75)] //走模糊匹配
    [TestCase("近月少女礼仪", 0.5)] //阈值放低，结果更多
    [TestCase("senren * banka", 0.75)] //归一化之后才能对上
    [TestCase("这个游戏肯定不存在", 0.75)] //查不到
    public async Task TryGetMapWithNameCacheTest(string name, double minSimilarity)
    {
        var noCache = await _mapper.TryGetMapsWithName(name, minSimilarity);
        var cached = await _mapper.TryGetMapsWithNameCache(name, minSimilarity);
        Assert.That(cached.Select(map => (map.model.VndbId, map.similarity)),
            Is.EqualTo(noCache.Select(map => (map.model.VndbId, map.similarity))));
    }

    // 默认的接口不该碰到索引，没预热过也得能直接查
    [Test]
    public async Task TryGetMapWithNameWithoutIndexTest()
    {
        var mapper = new VnDbMapper();
        mapper.Init("vn_mapper.db");
        var result = await mapper.TryGetMapsWithName("近月少女的礼仪");
        Assert.That(result.Any(map => map.model.VndbId == 10680));
        mapper.Dispose();
    }

    // 索引只建一次，之后的搜索都在内存里做，不用再把十几万行的title表读一遍
    [Test]
    public async Task TryGetMapWithNameCacheIsFastTest()
    {
        await _mapper.PreloadTitlesAsync();
        var watch = Stopwatch.StartNew();
        await _mapper.TryGetMapsWithNameCache("近月少女礼仪"); //不走精确匹配，真扫一遍索引
        Assert.That(watch.ElapsedMilliseconds, Is.LessThan(1000));
    }

    [OneTimeTearDown]
    public void Clean()
    {
        _mapper.Dispose();
    }
}
