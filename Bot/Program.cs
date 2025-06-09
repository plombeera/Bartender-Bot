using Bot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using System;

Host.CreateDefaultBuilder(args)

    .ConfigureAppConfiguration(cfg =>
        cfg.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true))

    .ConfigureServices((ctx, services) =>
    {
        services.Configure<BotOptions>(ctx.Configuration);
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<BotOptions>>().Value;
            return new TelegramBotClient(opts.BotToken);
        });
        services.AddHttpClient<ApiClient>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<BotOptions>>().Value;
            http.BaseAddress = new Uri(opts.ApiBaseUrl);          // http://localhost:5000
        });
        services.AddHostedService<BotService>();
    })

    .Build()
    .Run();