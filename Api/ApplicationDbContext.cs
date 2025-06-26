using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api
{
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
