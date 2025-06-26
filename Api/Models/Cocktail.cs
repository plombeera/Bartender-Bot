namespace Api;

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