using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace Api.Services;
public sealed class WikipediaService
{
    private readonly HttpClient _http;

    public WikipediaService(IHttpClientFactory fab)
    {
        _http = fab.CreateClient(nameof(WikipediaService));
        _http.BaseAddress = new("https://en.wikipedia.org/api/rest_v1/");
    }

    public async Task<string?> GetSummaryAsync(string title, CancellationToken ct = default)
    {
        var url = $"page/summary/{Uri.EscapeDataString(title)}";
        using var resp = await _http.GetAsync(url, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound ||
            resp.StatusCode == HttpStatusCode.Gone ||
            (int)resp.StatusCode >= 500)
        {
            return null;
        }

        resp.EnsureSuccessStatusCode();
        var obj = await resp.Content.ReadFromJsonAsync<Resp>(cancellationToken: ct);

        if (obj is null || string.IsNullOrWhiteSpace(obj.extract))
            return null;

        var extractLower = obj.extract.ToLowerInvariant();
        if (extractLower.Contains("may refer to") || extractLower.Contains("refer to:"))
        {
            var disambigUrl = $"page/related/{Uri.EscapeDataString(title)}";
            using var relResp = await _http.GetAsync(disambigUrl, ct);
            relResp.EnsureSuccessStatusCode();
            var relObj = await relResp.Content.ReadFromJsonAsync<RelatedResp>(cancellationToken: ct);

            string[] keywords = new[]
            {
            "cocktail", "drink", "beverage", "alcohol", "liqueur", "spirit", "mixed",
            "punch", "shot", "sour", "highball", "lowball", "martini", "fizz", "sling",
            "cooler", "flip", "smash", "spritz", "colada", "daiquiri", "margarita",
            "mojito", "negroni", "manhattan", "old fashioned"
        };

            var match = relObj?.pages?.FirstOrDefault(
                p => keywords.Any(k => p.title.ToLowerInvariant().Contains(k)));

            if (match != null)
            {
                return await GetSummaryAsync(match.title, ct);
            }
            return null;
        }

        string[] mainKeywords = new[]
        {
        "cocktail", "drink", "beverage", "alcohol", "liqueur", "spirit", "mixed",
        "punch", "shot", "sour", "highball", "lowball", "martini", "fizz", "sling",
        "cooler", "flip", "smash", "spritz", "colada", "daiquiri", "margarita",
        "mojito", "negroni", "manhattan", "old fashioned"
    };
        if (!mainKeywords.Any(k => title.ToLowerInvariant().Contains(k)) &&
            !mainKeywords.Any(k => obj.extract.ToLowerInvariant().Contains(k)))
            return null;

        return obj.extract;
    }


    private record Resp(string extract);
    private record RelatedResp(List<RelatedPage> pages);
    private record RelatedPage(string title);

}
