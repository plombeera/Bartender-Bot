// File: Api/Controllers/CocktailsController.cs

using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CocktailsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly SpoonacularService _spoon;

    public CocktailsController(ApplicationDbContext db, SpoonacularService spoon)
    {
        _db = db;
        _spoon = spoon;
    }

    /*???????????????????????????? 1. RANDOM ????????????????????????????*/
    // GET /api/cocktails/random
    [HttpGet("random")]
    public async Task<ActionResult<Cocktail>> Random(
        [FromHeader(Name = "X-ChatId")] long? chatId)
    {
        var ext = await _spoon.GetRandomAsync();
        if (ext is null) return Problem("Spoonacular error");

        // ищем или добавл€ем коктейль
        var cocktail = await _db.Cocktails
            .FirstOrDefaultAsync(c => c.ExternalId == ext.id)
            ?? new Cocktail
            {
                ExternalId = ext.id,
                Name = ext.title,
                ImageUrl = ext.image,
                Instructions = ext.instructions
            };

        if (cocktail.Id == Guid.Empty)
        {
            _db.Cocktails.Add(cocktail);
            await _db.SaveChangesAsync();
        }

        // пишем в историю
        if (chatId.HasValue)
        {
            _db.Histories.Add(new History
            {
                ChatId = chatId.Value,
                CocktailId = cocktail.Id,
                ViewedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return cocktail;
    }

    /*???????????????????????????? 2. HISTORY ???????????????????????????*/
    // GET /api/cocktails/history?chatId=123&limit=10
    [HttpGet("history")]
    public async Task<IEnumerable<Cocktail>> History(
        [FromQuery] long chatId,
        [FromQuery] int limit = 10)
    {
        var ids = await _db.Histories.Where(h => h.ChatId == chatId)
                     .OrderByDescending(h => h.ViewedAt)
                     .Select(h => h.CocktailId)
                     .Take(limit)
                     .ToListAsync();

        return await _db.Cocktails.Where(c => ids.Contains(c.Id)).ToListAsync();
    }

    /*???????????????????????????? 3. RATE ??????????????????????????????*/
    // POST /api/cocktails/{id}/rate
    [HttpPost("{id:guid}/rate")]
    public async Task<IActionResult> Rate(
        [FromRoute] Guid id,
        [FromBody] int score,
        [FromHeader(Name = "X-ChatId")] long XChatId)
    {
        if (score is < 1 or > 5) return BadRequest("Score must be 1Ц5");

        _db.Ratings.Add(new Rating
        {
            ChatId = XChatId,
            CocktailId = id,
            Score = score
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /*???????????????????????????? 4. SEARCH ????????????????????????????*/
    // GET /api/cocktails/search?query=mojito&limit=5
    [HttpGet("search")]
    public async Task<IEnumerable<Cocktail>> Search(
        [FromQuery] string query,
        [FromQuery] int limit = 10)
        => await EnsureInDb(await _spoon.SearchAsync(query, limit));

    /*???????????????????????????? 5. FILTER ????????????????????????????*/
    // GET /api/cocktails/filter?tag=gin
    [HttpGet("filter")]
    public async Task<IEnumerable<Cocktail>> Filter(
        [FromQuery] string tag,
        [FromQuery] int limit = 10)
        => await EnsureInDb(await _spoon.FilterByTagAsync(tag, limit));

    /*?????????????????????? 6. BY INGREDIENTS ??????????????????????????*/
    // GET /api/cocktails/by-ingredients?list=rum,lime&limit=5
    [HttpGet("by-ingredients")]
    public async Task<IEnumerable<Cocktail>> ByIngredients(
        [FromQuery] string list,
        [FromQuery] int limit = 10)
    {
        var arr = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return await EnsureInDb(await _spoon.ByIngredientsAsync(arr, limit));
    }

    /*?????????????????????????? 7. GET BY ID ???????????????????????????*/
    // GET /api/cocktails/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Cocktail>> Get(Guid id)
        => await _db.Cocktails.FindAsync(id) is { } c ? c : NotFound();

    /*?????????????????????????? 8. COMPARE ?????????????????????????????*/
    public record CompareRequest(Guid id1, Guid id2);

    // POST /api/cocktails/compare
    [HttpPost("compare")]
    public async Task<ActionResult<object>> Compare([FromBody] CompareRequest rq)
    {
        var c1 = await _db.Cocktails.FindAsync(rq.id1);
        var c2 = await _db.Cocktails.FindAsync(rq.id2);
        if (c1 is null || c2 is null) return NotFound("One or both cocktails not found");
        return new { first = c1, second = c2 };
    }

    /*?????????????????????????? helper ????????????????????????????????*/
    private async Task<List<Cocktail>> EnsureInDb(IEnumerable<SpoonRecipe> recipes)
    {
        var result = new List<Cocktail>();

        foreach (var r in recipes)
        {
            var c = await _db.Cocktails.FirstOrDefaultAsync(x => x.ExternalId == r.id);
            if (c == null)
            {
                c = new Cocktail
                {
                    ExternalId = r.id,
                    Name = r.title,
                    ImageUrl = r.image,
                    Instructions = r.instructions
                };
                _db.Cocktails.Add(c);
            }
            result.Add(c);
        }

        await _db.SaveChangesAsync();
        return result;
    }
}
