
using Ct = System.Threading.CancellationToken;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot;

public sealed class BotService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly ApiClient _api;
    public BotService(ITelegramBotClient bot, ApiClient api) => (_bot, _api) = (bot, api);
    private enum Await { None, Search, Tag, Ing, CmpA, CmpB }
    private readonly Dictionary<long, Await> _wait = new();
    private readonly Dictionary<long, string?> _cmpBuf = new();

    private static readonly ReplyKeyboardMarkup KB = new(new[]
    {
        new KeyboardButton[] { new("🎲 Random"), new("📜 History"), new("⭐ Rated") },
        new KeyboardButton[] { new("🔍 Search"), new("🧩 Tag"),     new("🥄 Ingredients") },
        new KeyboardButton[] { new("🔀 Compare") }
    })
    { ResizeKeyboard = true };

    private static readonly Regex _html = new("<.*?>", RegexOptions.Compiled);

    protected override Task ExecuteAsync(Ct stop)
    {
        var opts = new ReceiverOptions
        {
            ThrowPendingUpdates = true 
        };

        _bot.StartReceiving(OnUpdate, OnError, receiverOptions: opts, cancellationToken: stop);
        Console.WriteLine("[Bot] started");
        return Task.CompletedTask;
    }

    private Task OnError(ITelegramBotClient _, Exception ex, Ct __)
    { Console.WriteLine(ex); return Task.CompletedTask; }

    private async Task OnUpdate(ITelegramBotClient _, Update upd, Ct ct)
    {
        if (upd.Type == UpdateType.Message && upd.Message!.Type == MessageType.Text)
            await OnMsg(upd.Message, ct);
        else if (upd.Type == UpdateType.CallbackQuery)
            await OnCb(upd.CallbackQuery!, ct);
    }
    private async Task OnMsg(Message m, Ct ct)
    {
        var chat = m.Chat.Id;
        var t = m.Text!.Trim();

        try
        {
            if (t == "/start")
            {
                await _bot.SendTextMessageAsync(chat, "*darova!*",
                                                parseMode: ParseMode.Markdown,
                                                replyMarkup: KB,
                                                cancellationToken: ct);
                _wait[chat] = Await.None;
                return;
            }

            if (t == "🎲 Random") { await Show(chat, await _api.Random(chat, ct), ct); return; }
            if (t == "📜 History") { await List(chat, await _api.History(chat, ct), "History:", ct); return; }
            if (t == "⭐ Rated") { await Table(chat, await _api.RatedTable(chat, ct), ct); return; }
            if (t == "🔍 Search") { await Ask(chat, "Cocktail name:", Await.Search, ct); return; }
            if (t == "🧩 Tag")
            {
                await Ask(chat, "Enter tag (for example: Vodka, Classic, Bitter):", Await.Tag, ct);
                return;
            }
            if (t == "🥄 Ingredients") { await Ask(chat, "Enter one ingradient:", Await.Ing, ct); return; }
            if (t == "🔀 Compare") { await Ask(chat, "First cocktail:", Await.CmpA, ct); return; }

            _wait.TryGetValue(chat, out var state);
            switch (state)
            {
                case Await.Search:
                    _wait[chat] = Await.None;
                    await List(chat, await _api.Search(t, 10, ct), "Results:", ct);
                    break;

                case Await.Tag:
                    _wait[chat] = Await.None;
                    await List(chat, await _api.Filter(t, 10, ct), $"Tag: {t}", ct);
                    break;

                case Await.Ing:
                    _wait[chat] = Await.None;
                    {
                        var cocktails = await _api.ByIng(t, 10, ct);
                        if (cocktails.Count == 0)
                            await _bot.SendTextMessageAsync(chat, "No cocktails found with those ingredients. Please try different ones.", cancellationToken: ct);
                        else
                            await List(chat, cocktails, "Matches:", ct);
                    }
                    break;

                case Await.CmpA:
                    _cmpBuf[chat] = t;
                    _wait[chat] = Await.CmpB;
                    await _bot.SendTextMessageAsync(chat, "Second cocktail:", cancellationToken: ct);
                    break;

                case Await.CmpB:
                    _wait[chat] = Await.None;
                    if (_cmpBuf.TryGetValue(chat, out var first))
                        await Compare(chat, first!, t, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            await _bot.SendTextMessageAsync(chat, "An error occurred. Please try again later.", cancellationToken: ct);
            Console.WriteLine($"[Bot Error] {ex}");
        }
    }

    private async Task OnCb(CallbackQuery cb, Ct ct)
    {
        var chat = cb.Message!.Chat.Id;
        var p = cb.Data!.Split('|');

        if (p[0] == "v")
        {
            var name = p[1];
            var hit = (await _api.Search(name, 1, ct)).FirstOrDefault();
            if (hit != null) await Show(chat, hit, ct);
        }
        else if (p[0] == "tag")
        {
            var tag = p[1];
            var items = await _api.Filter(tag, 10, ct);
            await List(chat, items, $"Tag: {tag}", ct);
            await _bot.AnswerCallbackQueryAsync(cb.Id, cancellationToken: ct);
        }

        else if (p[0] == "r")
        {
            if (p.Length < 3 || !Guid.TryParse(p[1], out var cocktailId) || !int.TryParse(p[2], out var score))
            {
                await _bot.AnswerCallbackQueryAsync(cb.Id, "Invalid data", cancellationToken: ct);
                return;
            }
            await _api.Rate(cocktailId, score, chat, ct);
            await _bot.AnswerCallbackQueryAsync(cb.Id, "Rating received", cancellationToken: ct);
        }

    }

    private Task Ask(long chat, string prompt, Await next, Ct ct)
    {
        _wait[chat] = next;
        return _bot.SendTextMessageAsync(chat, prompt, cancellationToken: ct);
    }

    private async Task List(long chat, IReadOnlyList<Cocktail> items, string title, Ct ct)
    {
        if (items.Count == 0)
        {
            await _bot.SendTextMessageAsync(chat, "Nothing found.", cancellationToken: ct);
            return;
        }

        var rows = items
            .Select(c => InlineKeyboardButton.WithCallbackData(c.Name, $"v|{c.Name}"))
            .Chunk(2);

        await _bot.SendTextMessageAsync(chat, title,
                                        replyMarkup: new InlineKeyboardMarkup(rows),
                                        cancellationToken: ct);
    }

    private async Task Table(long chat, IReadOnlyList<ApiClient.RatedRow> rows, Ct ct)
    {
        if (rows.Count == 0)
        {
            await _bot.SendTextMessageAsync(chat, "You haven’t rated anything.", cancellationToken: ct);
            return;
        }

        var buttons = rows
            .Select(r => InlineKeyboardButton.WithCallbackData($"{r.Name} | ★{r.Stars}", $"v|{r.Name}"))
            .Select(btn => new[] { btn })
            .ToArray();

        await _bot.SendTextMessageAsync(chat, "Your rated cocktails:",
                                        replyMarkup: new InlineKeyboardMarkup(buttons),
                                        cancellationToken: ct);
    }

    private async Task Show(long chat, Cocktail c, Ct ct)
    {
        var fullText = Format(c);
        var captionLimit = 1024;
        string caption;
        if (fullText.Length > captionLimit)
        {
            caption = fullText[..captionLimit];
            var msg = await _bot.SendPhotoAsync(chat, c.ImageUrl, caption,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(
                    Enumerable.Range(1, 5)
                        .Select(i => InlineKeyboardButton.WithCallbackData($"⭐{i}", $"r|{c.Id}|{i}"))
                        .Chunk(5)),
                cancellationToken: ct);

            var rest = fullText.Substring(captionLimit);
            await _bot.SendTextMessageAsync(chat, rest,
                parseMode: ParseMode.Html,
                disableWebPagePreview: true,
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendPhotoAsync(chat, c.ImageUrl, fullText,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(
                    Enumerable.Range(1, 5)
                        .Select(i => InlineKeyboardButton.WithCallbackData($"⭐{i}", $"r|{c.Id}|{i}"))
                        .Chunk(5)),
                cancellationToken: ct);
        }
    }

    private async Task Compare(long chat, string a, string b, Ct ct)
    {
        var listA = await _api.Search(a, 10, ct);
        var listB = await _api.Search(b, 10, ct);
        var c1 = listA.FirstOrDefault(x => x.Name.Contains(a, StringComparison.OrdinalIgnoreCase))
                 ?? listA.FirstOrDefault();
        var c2 = listB.FirstOrDefault(x => x.Name.Contains(b, StringComparison.OrdinalIgnoreCase))
                 ?? listB.FirstOrDefault();

        if (c1 is null || c2 is null)
        {
            await _bot.SendTextMessageAsync(chat, "One of those wasn’t found.", cancellationToken: ct);
            return;
        }

        var msg =
            $"*{c1.Name}* vs *{c2.Name}*\n" +
            $"• Instruction length : {c1.Instructions.Length} vs {c2.Instructions.Length}\n" +
            $"• Ingredients count  : {c1.Ingredients.Length} vs {c2.Ingredients.Length}";
        await _bot.SendTextMessageAsync(chat, msg,
                                        parseMode: ParseMode.Markdown,
                                        disableWebPagePreview: true,
                                        cancellationToken: ct);
    }

    private static string Esc(string t)
        => t.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    private static string Strip(string html) => _html.Replace(html, " ").Trim();

    private static string Format(Cocktail c)
    {
        var sb = new StringBuilder($"<b>{Esc(c.Name)}</b>\n\n")
            .Append("<b>How to make:</b>\n")
            .Append(Esc(Strip(c.Instructions))).Append("\n\n");

        if (c.Ingredients.Length > 0)
        {
            sb.Append("<b>Ingredients:</b>\n");
            foreach (var ing in c.Ingredients)
                sb.Append("• ").Append(Esc(ing)).Append('\n');
            sb.Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(c.Summary))
        {
            var url = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(c.Name.Replace(' ', '_'))}";
            sb.Append("<b>Wikipedia:</b>\n")
              .Append(Esc(c.Summary)).Append('\n')
              .Append($"<a href=\"{url}\">Read more</a>\n");
        }
        else
            sb.Append("<i>No info on wiki.</i>\n");

        return sb.ToString();
    }
}
