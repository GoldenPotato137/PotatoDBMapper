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
    [TestCase("近月少女的礼仪", 10680)]
    [TestCase("大图书馆的牧羊人 -Dreaming Sheep-", 12480)]
    public async Task TryGetMapWithName(string name, int targetVndbId)
    {
        var result = await _mapper.TryGetMapsWithName(name);
        Assert.That(result.Any(map => map.model.VndbId == targetVndbId));
    }
        
    [OneTimeTearDown]
    public void Clean()
    {
        _mapper.Dispose();
    }
}