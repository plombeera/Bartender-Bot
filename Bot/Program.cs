using Bot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        var conf = ctx.Configuration;

        services.AddSingleton<ITelegramBotClient>(
            _ => new TelegramBotClient(conf["BotToken"]!));

        services.AddHttpClient<ApiClient>(c =>
            c.BaseAddress = new Uri(conf["ApiBaseUrl"]!));

        services.AddHostedService<BotService>();
    })
    .Build()
    .Run();