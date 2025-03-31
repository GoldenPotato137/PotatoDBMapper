namespace NugetPackage.Test;

[TestFixture]
public class VnDbMapperTest
{
    private readonly VnDbMapper _mapper = new VnDbMapper();
        
    [OneTimeSetUp]
    public void Init()
    {
        _mapper.Init();
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
    [TestCase(44123, 1)]
    [TestCase(406121, 3)]
    public async Task TryGetMapWithBgmId(int bgmId, int targetCount)
    {
        var result = await _mapper.TryGetMapsWithBgmId(bgmId);
        Assert.That(result.Count == targetCount);
    }

    [Test]
    [TestCase("近月少女的礼仪", 10680)]
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