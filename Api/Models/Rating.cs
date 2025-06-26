namespace Api;

public sealed class Rating
{
    public Guid Id { get; set; }
    public long ChatId { get; set; }
    public Guid CocktailId { get; set; }
    public int Score { get; set; }  
}