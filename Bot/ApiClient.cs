
using Ct = System.Threading.CancellationToken;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Bot;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    private void SetChatHeader(long chat)
    {
        const string h = "X-ChatId";
        _http.DefaultRequestHeaders.Remove(h);
        _http.DefaultRequestHeaders.Add(h, chat.ToString());
    }

    public Task<Cocktail> Random(long chat, Ct ct = default)
    {
        SetChatHeader(chat);
        return _http.GetFromJsonAsync<Cocktail>("api/cocktails/random", ct)!;
    }

    public async Task<IReadOnlyList<Cocktail>> History(long chat, Ct ct = default)
        => await _http.GetFromJsonAsync<IReadOnlyList<Cocktail>>($"api/cocktails/history?chatId={chat}", ct)
           ?? Array.Empty<Cocktail>();

    public async Task<IReadOnlyList<Cocktail>> Rated(long chat, Ct ct = default)
        => await _http.GetFromJsonAsync<IReadOnlyList<Cocktail>>($"api/cocktails/rated?chatId={chat}", ct)
           ?? Array.Empty<Cocktail>();

    public Task<IReadOnlyList<RatedRow>> RatedTable(long chat, Ct ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<RatedRow>>($"api/cocktails/rated/table?chatId={chat}", ct)!;

    public Task<IReadOnlyList<Cocktail>> Search(string q, int l, Ct ct)
        => _http.GetFromJsonAsync<IReadOnlyList<Cocktail>>($"api/cocktails/search?query={Uri.EscapeDataString(q)}&limit={l}", ct)!;

    public Task<IReadOnlyList<Cocktail>> Filter(string tag, int l, Ct ct)
        => _http.GetFromJsonAsync<IReadOnlyList<Cocktail>>($"api/cocktails/filter?tag={Uri.EscapeDataString(tag)}&limit={l}", ct)!;

    public Task<IReadOnlyList<Cocktail>> ByIng(string csv, int l, Ct ct)
        => _http.GetFromJsonAsync<IReadOnlyList<Cocktail>>($"api/cocktails/by-ingredients?list={Uri.EscapeDataString(csv)}&limit={l}", ct)!;

    public Task Rate(Guid id, int score, long chat, Ct ct)
    {
        SetChatHeader(chat);
        return _http.PostAsJsonAsync($"api/cocktails/{id}/rate", score, ct);
    }
    public Task<IReadOnlyList<string>> PopularTags(int limit, Ct ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<string>>($"api/cocktails/tags/popular?limit={limit}", ct)!;

    public record RatedRow(string Name, int Stars);
}

public record Cocktail(Guid Id, string Name, string ImageUrl,
                       string Instructions, string? Summary, string[] Ingredients);
