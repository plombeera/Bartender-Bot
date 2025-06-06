// File: Api/ApplicationDbContext.cs

using Microsoft.EntityFrameworkCore;

namespace Api
{
    // ── Модели ───────────────────────────────────────────────────────────────────
    public class Cocktail
    {
        public Guid Id { get; set; }
        public int ExternalId { get; set; }
        public string Name { get; set; } = null!;
        public string Instructions { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
    }

    public class History
    {
        public Guid Id { get; set; }
        public long ChatId { get; set; }
        public Guid CocktailId { get; set; }
        public DateTime ViewedAt { get; set; }
    }

    public class Rating
    {
        public Guid Id { get; set; }
        public long ChatId { get; set; }
        public Guid CocktailId { get; set; }
        public int Score { get; set; } // 1..5
    }

    // ── Контекст базы данных ───────────────────────────────────────────────────────
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Cocktail> Cocktails => Set<Cocktail>();
        public DbSet<History> Histories => Set<History>();
        public DbSet<Rating> Ratings => Set<Rating>();
    }
}