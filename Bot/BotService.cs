using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.InputFiles;

namespace Bot;

public class BotService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly ApiClient _api;

    public BotService(ITelegramBotClient bot, ApiClient api)
    {
        _bot = bot;
        _api = api;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _bot.StartReceiving(UpdateHandler, ErrorHandler, cancellationToken: stoppingToken);
        var me = await _bot.GetMeAsync(stoppingToken);
        Console.WriteLine($"[Bot] @{me.Username} started");
    }

    /* ───────── Update handler ───────── */

    private async Task UpdateHandler(ITelegramBotClient _, Update upd, CancellationToken ct)
    {
        if (upd.Type != UpdateType.Message || upd.Message!.Type != MessageType.Text) return;

        long chatId = upd.Message.Chat.Id;
        string[] cmd = upd.Message.Text.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        switch (cmd[0])
        {
            case "/start":
                await _bot.SendTextMessageAsync(chatId,
                    "🍸 *Бот-бармен*\n" +
                    "/random — случайный\n" +
                    "/history — последние 10\n" +
                    "/rate <id> <1-5>\n" +
                    "/search <text>\n" +
                    "/filter <tag>\n" +
                    "/ingredients <ing1,ing2>\n" +
                    "/compare <id1> <id2>",
                    ParseMode.Markdown);
                break;

            case "/random":
                if (await _api.GetRandomAsync(chatId) is { } c)
                    await _bot.SendPhotoAsync(chatId,
                        photo: new InputOnlineFile(c.ImageUrl),
                        caption: $"*{c.Name}*\n\n{Short(c.Instructions)}\n\n`{c.Id}`",
                        parseMode: ParseMode.Markdown);
                else
                    await _bot.SendTextMessageAsync(chatId, "❌ Ошибка API");
                break;

            case "/history":
                await SendList(chatId, await _api.GetHistoryAsync(chatId), "🕑 История:");
                break;

            case "/rate" when cmd.Length == 3
                           && Guid.TryParse(cmd[1], out var rid)
                           && int.TryParse(cmd[2], out var score):
                try
                {
                    await _api.RateAsync(rid, score, chatId);
                    await _bot.SendTextMessageAsync(chatId, "👍 Сохранено");
                }
                catch { await _bot.SendTextMessageAsync(chatId, "❌ Не удалось"); }
                break;

            case "/search" when cmd.Length >= 2:
                await SendList(chatId, await _api.SearchAsync(string.Join(' ', cmd.Skip(1))),
                               "🔍 Результаты:");
                break;

            case "/filter" when cmd.Length == 2:
                await SendList(chatId, await _api.FilterAsync(cmd[1]), $"🎯 Фильтр {cmd[1]}:");
                break;

            case "/ingredients" when cmd.Length == 2:
                await SendList(chatId, await _api.ByIngredientsAsync(cmd[1]),
                               $"🧩 Ингредиенты {cmd[1]}:");
                break;

            case "/compare" when cmd.Length == 3
                               && Guid.TryParse(cmd[1], out var id1)
                               && Guid.TryParse(cmd[2], out var id2):
                try
                {
                    var (a, b) = await _api.CompareAsync(id1, id2);
                    await _bot.SendTextMessageAsync(chatId,
                        $"*{a.Name}* vs *{b.Name}*\n`{a.Id}` 🆚 `{b.Id}`",
                        ParseMode.Markdown);
                }
                catch { await _bot.SendTextMessageAsync(chatId, "❌ Не найдено"); }
                break;

            default:
                await _bot.SendTextMessageAsync(chatId, "Неизвестная команда. /start");
                break;
        }
    }

    private Task ErrorHandler(ITelegramBotClient _, Exception ex, CancellationToken __)
    {
        Console.WriteLine(ex is ApiRequestException api
            ? $"[Telegram API] {api.ErrorCode}: {api.Message}"
            : ex);
        return Task.CompletedTask;
    }

    /* ───────── small helpers ───────── */

    private async Task SendList(long chatId, IEnumerable<CocktailDto> list, string header)
    {
        var txt = list.Any()
            ? header + "\n\n" + string.Join('\n', list.Select(c => $"• {c.Name} — `{c.Id}`"))
            : "🚫 Ничего не найдено";

        await _bot.SendTextMessageAsync(chatId, txt, ParseMode.Markdown);
    }

    private static string Short(string s) => s.Length <= 512 ? s : s[..509] + "...";
}
