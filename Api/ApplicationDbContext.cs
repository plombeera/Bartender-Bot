using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api
{
    public sealed class Cocktail
    {
        public Guid Id { get; set; }
        public int ExternalId { get; set; }
        public string Name { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public string Instructions { get; set; } = null!;
        public string[] Ingredients { get; set; } = Array.Empty<string>();
        public string? Summary { get; set; }
    }

    public sealed class History
    {
        public Guid Id { get; set; }
        public long ChatId { get; set; }
        public Guid CocktailId { get; set; }
        public DateTime ViewedAt { get; set; }
    }

    public sealed class Rating
    {
        public Guid Id { get; set; }
        public long ChatId { get; set; }
        public Guid CocktailId { get; set; }
        public int Score { get; set; }  
    }

    public sealed class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> o) : base(o) { }

        public DbSet<Cocktail> Cocktails => Set<Cocktail>();
        public DbSet<History> Histories => Set<History>();
        public DbSet<Rating> Ratings => Set<Rating>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            var conv = new ValueConverter<string[], string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null)!,
                v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null)!);

            mb.Entity<Cocktail>()
              .Property(c => c.Ingredients)
              .HasConversion(conv);
        }
    }
}
