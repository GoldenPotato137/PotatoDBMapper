using System.Net.Http.Headers;
using System.Web;
using Newtonsoft.Json.Linq;

namespace PotatoDBMapper;

public class BgmClient
{
    private readonly HttpClient _httpClient;

    public BgmClient()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "GoldenPotato/PotatoDBMapper (https://github.com/GoldenPotato137/PotatoDBMapper)");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<(int,double)> GetId(string name)
    {
        try
        {
            var url = "https://api.bgm.tv/search/subject/" + HttpUtility.UrlEncode(name) + "?type=4";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (0,0);
            var jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            var games = jsonToken["list"]!.ToObject<List<JToken>>();
            if (games == null || games.Count == 0) return (-1,0);

            double maxSimilarity = 0;
            var target = 0;
            foreach (var game in games)
                if (name.Similarity(game["name_cn"]!.ToObject<string>()!) > maxSimilarity ||
                    name.Similarity(game["name"]!.ToObject<string>()!) > maxSimilarity)
                {
                    maxSimilarity = Math.Max
                    (
                        name.Similarity(game["name_cn"]!.ToObject<string>()!),
                        name.Similarity(game["name"]!.ToObject<string>()!)
                    );
                    target = games.IndexOf(game);
                }
            return (games[target]["id"]!.ToObject<int>(), maxSimilarity);
        }
        catch // 解析错误，应该是拥挤控制了
        {
            return (0,0);
        }
    }
}