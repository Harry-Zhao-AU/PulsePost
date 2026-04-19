using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PulsePost.Functions.Services;

public class PublishService(HttpClient httpClient, ILogger<PublishService> logger) : IPublishService
{
    private readonly string _pat = Environment.GetEnvironmentVariable("GITHUB_PAT")!;
    private const string BlogRepo = "Harry-Zhao-AU/harry-zhao-au.github.io";

    public async Task CreateBlogPrAsync(string title, string article, string imagePrompt, CancellationToken ct = default)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _pat);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PulsePost/1.0");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var slug = GenerateSlug(title);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var path = $"_posts/{date}-{slug}.md";

        var frontMatter = $"""
            ---
            layout: post
            title: "{title}"
            date: {date}
            categories: [AI, Engineering]
            channel: trends
            ---

            """;

        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(frontMatter + article));

        var createFilePayload = new
        {
            message = $"Add article: {title}",
            content,
            branch = "main"
        };

        var createResponse = await httpClient.PutAsync(
            $"https://api.github.com/repos/{BlogRepo}/contents/{path}",
            new StringContent(JsonSerializer.Serialize(createFilePayload), Encoding.UTF8, "application/json"),
            ct);

        if (!createResponse.IsSuccessStatusCode)
        {
            logger.LogError("Failed to create file: {Status}", createResponse.StatusCode);
            return;
        }

        var prPayload = new
        {
            title = $"Article: {title}",
            body = $"## New Article\n\n**Image Prompt:**\n```\n{imagePrompt}\n```\n\nGenerate image, upload to `assets/images/`, update front matter, then merge.",
            head = "main",
            base_ = "main"
        };

        var prResponse = await httpClient.PostAsync(
            $"https://api.github.com/repos/{BlogRepo}/pulls",
            new StringContent(JsonSerializer.Serialize(prPayload), Encoding.UTF8, "application/json"),
            ct);

        logger.LogInformation("Blog PR created: {Status}", prResponse.StatusCode);
    }

    private static string GenerateSlug(string title) =>
        System.Text.RegularExpressions.Regex.Replace(title.ToLower(), @"[^a-z0-9]+", "-").Trim('-');
}
