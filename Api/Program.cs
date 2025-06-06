using Api;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// слухаємо тільки http://localhost:5000
builder.WebHost.UseUrls("http://localhost:5000");

// ▸ ▸ ▸  конфігурація  ◂ ◂ ◂
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // плейсхолдер (може бути відсутній)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)   // для локальної розробки
    .AddEnvironmentVariables();               // для Docker/CI

// ▸ ▸ ▸  DI   ◂ ◂ ◂
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlite("Data Source=cocktails.db"));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<SpoonacularService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CocktailMaster API",
        Version = "v1"
    });
});

var app = builder.Build();

// ▸ ▸ ▸  middleware  ◂ ◂ ◂
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
        c.RoutePrefix = string.Empty; // Swagger за адресою /
    });
}

// НЕ робимо HTTPS-redirect, щоби бот ходив по http:5000
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();