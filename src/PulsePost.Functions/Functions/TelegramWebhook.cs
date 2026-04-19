using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PulsePost.Functions.Models;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Functions;

public class TelegramWebhook(
    IDraftStorageService storage,
    IOpenAIService openAI,
    ITelegramService telegram,
    ServiceBusClient serviceBusClient,
    ILogger<TelegramWebhook> logger)
{
    private readonly string _allowedChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")!;

    [Function(nameof(TelegramWebhook))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        CancellationToken ct)
    {
        var body = await JsonSerializer.DeserializeAsync<TelegramUpdate>(req.Body, cancellationToken: ct);
        if (body?.Message is null) return new OkResult();

        if (body.Message.Chat.Id.ToString() != _allowedChatId)
        {
            logger.LogWarning("Ignored message from unknown chat: {Id}", body.Message.Chat.Id);
            return new OkResult();
        }

        var text = body.Message.Text.Trim();
        logger.LogInformation("Telegram command received: {Text}", text);

        if (text == "/generate")
        {
            var sender = serviceBusClient.CreateSender("fetch-topics");
            await sender.SendMessageAsync(new ServiceBusMessage("start"), ct);
            await telegram.SendMessageAsync("🚀 Pipeline started! You'll receive the draft shortly.", ct);
            return new OkResult();
        }

        var draft = await storage.GetLatestPendingAsync(ct);
        if (draft is null)
        {
            await telegram.SendMessageAsync("⚠️ No pending draft found.", ct);
            return new OkResult();
        }

        if (text.Equals("APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            var sender = serviceBusClient.CreateSender("post-article");
            await sender.SendMessageAsync(new ServiceBusMessage(draft.RowKey), ct);
            await telegram.SendMessageAsync("✅ Publishing...", ct);
        }
        else if (text.Equals("REJECT", StringComparison.OrdinalIgnoreCase))
        {
            await storage.UpdateStatusAsync(draft.RowKey, "rejected", ct);
            await telegram.SendMessageAsync("❌ Draft rejected and discarded.", ct);
        }
        else if (text.StartsWith("EDIT", StringComparison.OrdinalIgnoreCase))
        {
            var feedback = text[4..].Trim();
            if (string.IsNullOrEmpty(feedback))
            {
                await telegram.SendMessageAsync("⚠️ Please provide feedback. Example: `EDIT make it more technical`", ct);
                return new OkResult();
            }

            await telegram.SendMessageAsync("✏️ Rewriting... please wait.", ct);

            var revised = await openAI.RewriteArticleAsync(draft.Article, feedback, ct);
            var xThreadJson = await openAI.GenerateXThreadAsync(revised, ct);
            var xThread = JsonSerializer.Deserialize<JsonElement>(xThreadJson);

            draft.Article = revised;
            draft.XThread = xThreadJson;
            await storage.UpdateStatusAsync(draft.RowKey, "pending", ct);

            var tweets = xThread.GetProperty("tweets").EnumerateArray()
                .Take(2).Select(t => $"• {t.GetString()}");

            await telegram.SendMessageAsync($"""
                ✏️ *Draft Revised*

                Feedback applied: _{feedback}_

                *X Thread Preview:*
                {string.Join("\n", tweets)}

                Reply: `APPROVE` / `REJECT` / `EDIT <more feedback>`
                """, ct);
        }
        else
        {
            await telegram.SendMessageAsync("Unknown command. Reply: `APPROVE` / `REJECT` / `EDIT <feedback>`", ct);
        }

        return new OkResult();
    }
}
