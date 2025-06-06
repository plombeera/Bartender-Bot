using System.Net.Http.Json;

namespace Bot;

public class CocktailDto
{
    public Guid Id { get; set; }
    public int ExternalId { get; set; }
    public string Name { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string ImageUrl { get; set; } = "";
}

public sealed class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public async Task<CocktailDto?> GetRandomAsync(long chatId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "api/cocktails/random");
        req.Headers.Add("X-ChatId", chatId.ToString());
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CocktailDto>();
    }

    public async Task<IEnumerable<CocktailDto>> GetHistoryAsync(long chatId) =>
        await _http.GetFromJsonAsync<IEnumerable<CocktailDto>>(
                 $"api/cocktails/history?chatId={chatId}&limit=10")
        ?? Enumerable.Empty<CocktailDto>();

    public async Task RateAsync(Guid id, int score, long chatId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"api/cocktails/{id}/rate")
        {
            Content = JsonContent.Create(score)
        };
        req.Headers.Add("X-ChatId", chatId.ToString());
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<CocktailDto>> SearchAsync(string q) =>
        await _http.GetFromJsonAsync<IEnumerable<CocktailDto>>
               ($"api/cocktails/search?query={Uri.EscapeDataString(q)}&limit=5")
        ?? Enumerable.Empty<CocktailDto>();

    public async Task<IEnumerable<CocktailDto>> FilterAsync(string tag) =>
        await _http.GetFromJsonAsync<IEnumerable<CocktailDto>>
               ($"api/cocktails/filter?tag={Uri.EscapeDataString(tag)}&limit=5")
        ?? Enumerable.Empty<CocktailDto>();

    public async Task<IEnumerable<CocktailDto>> ByIngredientsAsync(string list) =>
        await _http.GetFromJsonAsync<IEnumerable<CocktailDto>>
               ($"api/cocktails/by-ingredients?list={Uri.EscapeDataString(list)}&limit=5")
        ?? Enumerable.Empty<CocktailDto>();

    public async Task<(CocktailDto first, CocktailDto second)> CompareAsync(Guid id1, Guid id2)
    {
        var resp = await _http.PostAsJsonAsync("api/cocktails/compare", new { id1, id2 });
        resp.EnsureSuccessStatusCode();
        var obj = await resp.Content.ReadFromJsonAsync<CompareDto>();
        return (obj!.first, obj.second);
    }

    private class CompareDto
    {
        public CocktailDto first { get; set; } = default!;
        public CocktailDto second { get; set; } = default!;
    }
}
