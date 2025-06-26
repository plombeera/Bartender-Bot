using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Services;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CocktailsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CocktailService _src;
    private readonly WikipediaService _wiki;

    public CocktailsController(ApplicationDbContext db, CocktailService src, WikipediaService wiki)
        => (_db, _src, _wiki) = (db, src, wiki);

    [HttpGet("random")]
    public async Task<ActionResult<Cocktail>> Random([FromHeader(Name = "X-ChatId")] long? chatId)
    {
        var ext = await _src.GetRandomAsync();
        if (ext is null) return Problem("Provider error");

        var c = await EnsureAsync(ext);
        await TrackAsync(c.Id, chatId);
        return c;
    }
    [HttpGet("history")]
    public async Task<IEnumerable<Cocktail>> History(long chatId, int limit = 10)
    {
        var history = await (
            from h in _db.Histories
            join c in _db.Cocktails on h.CocktailId equals c.Id
            where h.ChatId == chatId
            orderby h.ViewedAt descending
            select c)
            .Take(limit)
            .ToListAsync();

        return history;
    }

    [HttpGet("rated")]
    public async Task<IEnumerable<Cocktail>> Rated(long chatId)
    {
        var ids = await _db.Ratings.Where(r => r.ChatId == chatId)
                                   .OrderByDescending(r => r.Score)
                                   .Select(r => r.CocktailId)
                                   .Distinct()
                                   .ToListAsync();
        return await _db.Cocktails.Where(c => ids.Contains(c.Id)).ToListAsync();
    }

[HttpGet("rated/table")]
public async Task<IEnumerable<RatedRow>> RatedTable(long chatId)
{
    var agg = await _db.Ratings
        .Where(r => r.ChatId == chatId)
        .GroupBy(r => r.CocktailId)
        .Select(g => new { g.Key, Stars = g.Max(x => x.Score) })
        .ToListAsync();

    if (agg.Count == 0) return Array.Empty<RatedRow>();

    var ids = agg.Select(a => a.Key).ToList();
    var map = agg.ToDictionary(a => a.Key, a => a.Stars);

    var cocktails = await _db.Cocktails
        .Where(c => ids.Contains(c.Id))
        .ToListAsync();

    var rows = cocktails
        .Where(c => map.ContainsKey(c.Id))
        .Select(c => new RatedRow(c.Name, map[c.Id]))
        .OrderByDescending(r => r.Stars)
        .ToList();

    return rows;
}
    [HttpPost("{id:guid}/rate")]
    public async Task<IActionResult> Rate(Guid id, [FromBody] int score,
        [FromHeader(Name = "X-ChatId")] long chatId)
    {
        if (score is < 1 or > 5)
            return BadRequest("Score 1–5 only");

        var existingRating = await _db.Ratings
            .FirstOrDefaultAsync(r => r.ChatId == chatId && r.CocktailId == id);

        if (existingRating is not null)
        {
            existingRating.Score = score;
            _db.Ratings.Update(existingRating);
        }
        else
        {
            _db.Ratings.Add(new Rating { ChatId = chatId, CocktailId = id, Score = score });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("search")]
    public async Task<IEnumerable<Cocktail>> Search(string query,
        [FromHeader(Name = "X-ChatId")] long? chatId,
        int limit = 10)
    {
        try
        {
            var extRecipes = await _src.SearchAsync(query, limit);
            var cocktails = await EnsureManyAsync(Task.FromResult(extRecipes));
            if (chatId is not null)
            {
                foreach (var cocktail in cocktails)
                {
                    await TrackAsync(cocktail.Id, chatId);
                }
            }
            return cocktails;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CocktailsController] Error in Search: {ex.Message}");
            return Array.Empty<Cocktail>();
        }
    }

    [HttpGet("filter")]
    public async Task<IEnumerable<Cocktail>> Filter(string tag, int limit = 10)
    {
        IEnumerable<CocktailService.ExtRecipe> recipes;
        try
        {
            recipes = await _src.FilterByTagAsync(tag, limit);
        }
        catch (Exception ex)
        {
            recipes = Array.Empty<CocktailService.ExtRecipe>();
        }

        if (!recipes.Any())
            recipes = await _src.SearchAsync(tag, limit);

        return await EnsureManyAsync(Task.FromResult(recipes));
    }


    [HttpGet("tags/popular")]
    public async Task<IEnumerable<string>> PopularTags([FromQuery] int limit = 10)
    {
        var ingredientsLists = await _db.Cocktails
            .Select(c => c.Ingredients)
            .Where(ings => ings != null)
            .ToListAsync();

        var popularTags = ingredientsLists
            .SelectMany(ings => ings!)
            .GroupBy(ing => ing)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(limit)
            .ToList();

        return popularTags;
    }


    [HttpGet("by-ingredients")]
    public Task<IEnumerable<Cocktail>> ByIngredients(string list, int limit = 10)
        => EnsureManyAsync(_src.ByIngredientsAsync(
               list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), limit));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Cocktail>> Get(Guid id)
        => await _db.Cocktails.FindAsync(id) is { } c ? c : NotFound();

    /* ───── helpers ───── */
    private async Task TrackAsync(Guid cocktailId, long? chatId)
    {
        if (chatId is null) return;
        _db.Histories.Add(new History { ChatId = chatId.Value, CocktailId = cocktailId, ViewedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    private async Task<Cocktail> EnsureAsync(CocktailService.ExtRecipe ext)
    {
        var existing = await _db.Cocktails.FirstOrDefaultAsync(c => c.ExternalId == ext.id);
        if (existing != null) return existing;

        var c = new Cocktail
        {
            ExternalId = ext.id,
            Name = ext.title,
            ImageUrl = ext.image,
            Instructions = ext.instructions,
            Ingredients = ext.ingredients.ToArray(),
            Summary = await _wiki.GetSummaryAsync(ext.title)
        };

        _db.Cocktails.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    private async Task<IEnumerable<Cocktail>> EnsureManyAsync(Task<IEnumerable<CocktailService.ExtRecipe>> src)
    {
        var list = new List<Cocktail>();
        foreach (var ext in await src)
            list.Add(await EnsureAsync(ext));
        return list;
    }
}

public record RatedRow(string Name, int Stars);
