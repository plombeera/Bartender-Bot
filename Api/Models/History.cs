namespace Api;

public sealed class History
{
    public Guid Id { get; set; }
    public long ChatId { get; set; }
    public Guid CocktailId { get; set; }
    public DateTime ViewedAt { get; set; }
}