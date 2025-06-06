// File: Api/Program.cs
using Api;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// слушаем ровно на http://localhost:5000
builder.WebHost.UseUrls("http://localhost:5000");

// ─── EF Core + SQLite ────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlite("Data Source=cocktails.db"));

// ─── HTTP-client + Spoonacular ───────────────────────
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SpoonacularService>();

// ─── Controllers + Swagger ───────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CocktailMaster API",
        Version = "v1",
        Description = "API для Telegram-бота бармена"
    });
});

var app = builder.Build();

// ─── Pipeline ────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CocktailMaster API v1");
        c.RoutePrefix = string.Empty; // Swagger по корню
    });
}

// НЕ редиректим, чтобы честно работать по http:5000
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();