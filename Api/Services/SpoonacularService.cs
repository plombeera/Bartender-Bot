// File: Api/Services/SpoonacularService.cs
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Api.Services;

// DTO для единичного рецепта (унифицированный вывод)
public record SpoonRecipe(int id, string title, string image, string instructions);

public class SpoonacularService
{
    private readonly HttpClient _http;
    private readonly string _key;

    public SpoonacularService(IConfiguration cfg, IHttpClientFactory f)
    {
        _key = cfg["Spoonacular:ApiKey"]!;
        _http = f.CreateClient();
        _http.BaseAddress = new Uri("https://api.spoonacular.com/");
    }

    /* ---------- helpers ---------- */
    private SpoonRecipe ToRecipe(JsonElement r) => new(
        r.GetProperty("id").GetInt32(),
        r.GetProperty("title").GetString()!,
        r.GetProperty("image").GetString()!,
        r.TryGetProperty("instructions", out var ins) && ins.GetString() is { } s && s.Length > 3
            ? s
            : "No instructions");

    /* ---------- public API ---------- */

    public async Task<SpoonRecipe?> GetRandomAsync()
    {
        var json = await _http.GetFromJsonAsync<JsonElement>($"recipes/random?number=1&tags=cocktail&apiKey={_key}");
        return json.ValueKind == JsonValueKind.Undefined ? null : ToRecipe(json.GetProperty("recipes")[0]);
    }

    public async Task<IEnumerable<SpoonRecipe>> SearchAsync(string query, int limit = 10)
    {
        var json = await _http.GetFromJsonAsync<JsonElement>(
            $"recipes/complexSearch?query={Uri.EscapeDataString(query)}&number={limit}&tags=cocktail&apiKey={_key}");
        return json.GetProperty("results").EnumerateArray().Select(ToRecipe);
    }

    public async Task<IEnumerable<SpoonRecipe>> FilterByTagAsync(string tag, int limit = 10)
    {
        var json = await _http.GetFromJsonAsync<JsonElement>(
            $"recipes/complexSearch?number={limit}&tags={Uri.EscapeDataString(tag)},cocktail&apiKey={_key}");
        return json.GetProperty("results").EnumerateArray().Select(ToRecipe);
    }

    public async Task<IEnumerable<SpoonRecipe>> ByIngredientsAsync(string[] ings, int limit = 10)
    {
        var joined = string.Join(',', ings.Select(Uri.EscapeDataString));
        var json = await _http.GetFromJsonAsync<JsonElement>(
            $"recipes/findByIngredients?ingredients={joined}&number={limit}&apiKey={_key}");
        return json.EnumerateArray().Select(r => new SpoonRecipe(
            r.GetProperty("id").GetInt32(),
            r.GetProperty("title").GetString()!,
            r.GetProperty("image").GetString()!,
            "")); // findByIngredients не возвращает instructions
    }
}
