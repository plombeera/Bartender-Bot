using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Api.Services;

public sealed class CocktailDbService
{
    private readonly HttpClient _http;

    public CocktailDbService(IConfiguration cfg, IHttpClientFactory fab)
    {
        var key = cfg["CocktailDB:ApiKey"] ?? "1";
        _http = fab.CreateClient(nameof(CocktailDbService));
        _http.BaseAddress = new($"https://www.thecocktaildb.com/api/json/v1/{key}/");
    }

    public Task<ExtRecipe?> GetRandomAsync(CancellationToken ct = default)
        => GetSingle("random.php", ct);

    public Task<IEnumerable<ExtRecipe>> SearchAsync(string q, int n, CancellationToken ct = default)
        => GetMany($"search.php?s={Uri.EscapeDataString(q)}", n, ct);

    public Task<IEnumerable<ExtRecipe>> FilterByTagAsync(string tag, int n, CancellationToken ct = default)
    {
        tag = tag.Trim();
        var url = tag.Equals("Alcoholic", StringComparison.OrdinalIgnoreCase) ||
                  tag.Equals("Non_Alcoholic", StringComparison.OrdinalIgnoreCase) ||
                  tag.Equals("Optional_alcohol", StringComparison.OrdinalIgnoreCase)
                  ? $"filter.php?a={Uri.EscapeDataString(tag)}"
                  : $"filter.php?c={Uri.EscapeDataString(tag)}";
        return FromFilter(url, n, ct);
    }

    public Task<IEnumerable<ExtRecipe>> ByIngredientsAsync(IEnumerable<string> list, int n, CancellationToken ct = default)
    {
        var ings = list.Select(i => i.Trim()).Where(i => i.Length > 0).ToArray();
        return ings.Length == 0
            ? Task.FromResult<IEnumerable<ExtRecipe>>(Array.Empty<ExtRecipe>())
            : ByIngredientsInternalAsync(ings, n, ct);
    }

    private async Task<ExtRecipe?> GetSingle(string url, CancellationToken ct)
        => (await GetMany(url, 1, ct)).FirstOrDefault();

    private async Task<IEnumerable<ExtRecipe>> GetMany(string url, int limit, CancellationToken ct)
    {
        try
        {
            var root = await _http.GetFromJsonAsync<Root>(url, ct);
            return root?.drinks?.Take(limit).Select(ToExt) ?? Array.Empty<ExtRecipe>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CocktailDbService] Error in GetMany with url '{url}': {ex.Message}");
            return Array.Empty<ExtRecipe>();
        }
    }


    private async Task<IEnumerable<ExtRecipe>> FromFilter(string url, int limit, CancellationToken ct)
    {
        var stub = await _http.GetFromJsonAsync<RootStub>(url, ct);
        if (stub?.drinks is null) return Array.Empty<ExtRecipe>();

        var ids = stub.drinks.Take(limit).Select(d => d.idDrink);
        var tasks = ids.Select(id => _http.GetFromJsonAsync<Root>($"lookup.php?i={id}", ct));
        var full = await Task.WhenAll(tasks);
        return full.SelectMany(r => r!.drinks ?? new()).Select(ToExt);
    }

    private async Task<IEnumerable<ExtRecipe>> ByIngredientsInternalAsync(string[] ings, int limit, CancellationToken ct)
    {
        var first = await _http.GetFromJsonAsync<RootStub>($"filter.php?i={Uri.EscapeDataString(ings[0])}", ct);
        if (first?.drinks is null) return Array.Empty<ExtRecipe>();

        var ids = first.drinks.Select(d => d.idDrink).ToList();
        foreach (var ing in ings.Skip(1))
        {
            var next = await _http.GetFromJsonAsync<RootStub>($"filter.php?i={Uri.EscapeDataString(ing)}", ct);
            var set = next?.drinks?.Select(d => d.idDrink).ToHashSet() ?? new();
            ids.RemoveAll(id => !set.Contains(id));
        }

        ids = ids.Take(limit).ToList();
        if (ids.Count == 0) return Array.Empty<ExtRecipe>();

        var tasks = ids.Select(id => _http.GetFromJsonAsync<Root>($"lookup.php?i={id}", ct));
        var full = await Task.WhenAll(tasks);
        return full.SelectMany(r => r!.drinks ?? new()).Select(ToExt);
    }

    private static ExtRecipe ToExt(Drink d)
    {
        var list = new List<string>();
        for (int i = 1; i <= 15; i++)
        {
            var ing = (string?)d.GetType().GetProperty($"Ingredient{i}")!.GetValue(d);
            var msr = (string?)d.GetType().GetProperty($"Measure{i}")!.GetValue(d);
            if (string.IsNullOrWhiteSpace(ing)) continue;
            list.Add(!string.IsNullOrWhiteSpace(msr)
                     ? $"{msr.Trim()} {ing.Trim()}"
                     : ing.Trim());
        }
        return new ExtRecipe(int.Parse(d.idDrink), d.strDrink,
                             d.strDrinkThumb ?? "", d.strInstructions ?? "—", list);
    }

    public record ExtRecipe(int id, string title, string image, string instructions, List<string> ingredients);

    private record Root(List<Drink>? drinks);
    private record RootStub(List<Stub>? drinks);
    private record Stub(string idDrink);
    private class Drink
    {
        [JsonPropertyName("idDrink")] public string idDrink { get; init; } = "";
        [JsonPropertyName("strDrink")] public string strDrink { get; init; } = "";
        [JsonPropertyName("strDrinkThumb")] public string? strDrinkThumb { get; init; }
        [JsonPropertyName("strInstructions")] public string? strInstructions { get; init; }
        [JsonPropertyName("strMeasure1")] public string? Measure1 { get; init; }
        [JsonPropertyName("strIngredient1")] public string? Ingredient1 { get; init; }
        [JsonPropertyName("strMeasure2")] public string? Measure2 { get; init; }
        [JsonPropertyName("strIngredient2")] public string? Ingredient2 { get; init; }
        [JsonPropertyName("strMeasure3")] public string? Measure3 { get; init; }
        [JsonPropertyName("strIngredient3")] public string? Ingredient3 { get; init; }
        [JsonPropertyName("strMeasure4")] public string? Measure4 { get; init; }
        [JsonPropertyName("strIngredient4")] public string? Ingredient4 { get; init; }
        [JsonPropertyName("strMeasure5")] public string? Measure5 { get; init; }
        [JsonPropertyName("strIngredient5")] public string? Ingredient5 { get; init; }
        [JsonPropertyName("strMeasure6")] public string? Measure6 { get; init; }
        [JsonPropertyName("strIngredient6")] public string? Ingredient6 { get; init; }
        [JsonPropertyName("strMeasure7")] public string? Measure7 { get; init; }
        [JsonPropertyName("strIngredient7")] public string? Ingredient7 { get; init; }
        [JsonPropertyName("strMeasure8")] public string? Measure8 { get; init; }
        [JsonPropertyName("strIngredient8")] public string? Ingredient8 { get; init; }
        [JsonPropertyName("strMeasure9")] public string? Measure9 { get; init; }
        [JsonPropertyName("strIngredient9")] public string? Ingredient9 { get; init; }
        [JsonPropertyName("strMeasure10")] public string? Measure10 { get; init; }
        [JsonPropertyName("strIngredient10")] public string? Ingredient10 { get; init; }
        [JsonPropertyName("strMeasure11")] public string? Measure11 { get; init; }
        [JsonPropertyName("strIngredient11")] public string? Ingredient11 { get; init; }
        [JsonPropertyName("strMeasure12")] public string? Measure12 { get; init; }
        [JsonPropertyName("strIngredient12")] public string? Ingredient12 { get; init; }
        [JsonPropertyName("strMeasure13")] public string? Measure13 { get; init; }
        [JsonPropertyName("strIngredient13")] public string? Ingredient13 { get; init; }
        [JsonPropertyName("strMeasure14")] public string? Measure14 { get; init; }
        [JsonPropertyName("strIngredient14")] public string? Ingredient14 { get; init; }
        [JsonPropertyName("strMeasure15")] public string? Measure15 { get; init; }
        [JsonPropertyName("strIngredient15")] public string? Ingredient15 { get; init; }
    }
}
