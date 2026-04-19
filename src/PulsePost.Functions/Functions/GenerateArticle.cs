using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PulsePost.Functions.Models;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Functions;

public class GenerateArticle(
    IOpenAIService openAI,
    IDraftStorageService storage,
    ITelegramService telegram,
    ILogger<GenerateArticle> logger)
{
    [Function(nameof(GenerateArticle))]
    public async Task Run(
        [ServiceBusTrigger("generate-article", Connection = "SERVICE_BUS_CONNECTION")] string topicsJson,
        CancellationToken ct)
    {
        logger.LogInformation("Generating article...");

        await storage.PurgeOlderThanAsync(7, ct);

        var topicJson = await openAI.SelectTopicAsync(topicsJson, ct);
        var topic = JsonSerializer.Deserialize<JsonElement>(topicJson);
        var topicTitle = topic.GetProperty("title").GetString() ?? "AI Article";

        logger.LogInformation("Selected topic: {Title}", topicTitle);

        var article = await openAI.GenerateArticleAsync(topicJson, ct);
        var xThreadJson = await openAI.GenerateXThreadAsync(article, ct);
        var xThread = JsonSerializer.Deserialize<JsonElement>(xThreadJson);
        var imagePrompt = await openAI.GenerateImagePromptAsync(article, topicTitle, ct);

        var draft = new ArticleDraft
        {
            Topic = topicTitle,
            Article = article,
            XThread = xThreadJson,
            ImagePrompt = imagePrompt,
            Sources = topic.TryGetProperty("sources", out var s) ? s.ToString() : "",
            Status = "pending",
        };

        await storage.SaveAsync(draft, ct);
        logger.LogInformation("Draft saved: {RowKey}", draft.RowKey);

        var tweets = xThread.GetProperty("tweets").EnumerateArray()
            .Take(2).Select(t => $"• {t.GetString()}");

        var sources = topic.TryGetProperty("sources", out var src)
            ? string.Join("\n", src.EnumerateArray().Select(s =>
                $"• [{s.GetProperty("name").GetString()}]({s.GetProperty("url").GetString()})"))
            : "";

        await telegram.SendMessageAsync($"""
            📝 *New Draft Ready*

            *Topic:* {topicTitle}

            *Sources:*
            {sources}

            *X Thread Preview:*
            {string.Join("\n", tweets)}

            *Image Prompt:*
            `{imagePrompt}`

            Reply: `APPROVE` / `REJECT` / `EDIT <feedback>`
            """, ct);
    }
}
