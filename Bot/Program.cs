using Bot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;

Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(c =>
        c.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables())
    .ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;

        services.AddSingleton<ITelegramBotClient>(_ =>
            new TelegramBotClient(cfg["BotToken"]!));

        services.AddHttpClient<ApiClient>(c =>
            c.BaseAddress = new Uri(cfg["ApiBaseUrl"]!));

        services.AddHostedService<BotService>();
    })
    .Build()
    .Run();