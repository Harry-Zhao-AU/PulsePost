using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Functions;

public class PostArticle(
    IDraftStorageService storage,
    IPublishService publish,
    ITelegramService telegram,
    ILogger<PostArticle> logger)
{
    [Function(nameof(PostArticle))]
    public async Task Run(
        [ServiceBusTrigger("post-article", Connection = "SERVICE_BUS_CONNECTION")] string rowKey,
        CancellationToken ct)
    {
        var draft = await storage.GetLatestPendingAsync(ct);
        if (draft is null)
        {
            logger.LogWarning("No pending draft found for rowKey: {RowKey}", rowKey);
            return;
        }

        logger.LogInformation("Publishing article: {Topic}", draft.Topic);

        await publish.CreateBlogPrAsync(draft.Topic, draft.Article, draft.ImagePrompt, ct);
        await storage.UpdateStatusAsync(draft.RowKey, "approved", ct);

        var xThread = JsonSerializer.Deserialize<JsonElement>(draft.XThread);
        var tweets = string.Join("\n\n", xThread.GetProperty("tweets")
            .EnumerateArray().Select((t, i) => $"*{i + 1}.* {t.GetString()}"));

        await telegram.SendMessageAsync($"""
            ✅ *Approved and published!*

            Blog PR created — add your image then merge.

            *X Thread — copy and post manually:*

            {tweets}

            *Image Prompt:*
            `{draft.ImagePrompt}`
            """, ct);
    }
}
