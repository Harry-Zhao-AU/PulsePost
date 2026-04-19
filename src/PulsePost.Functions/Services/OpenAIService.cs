using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;

namespace PulsePost.Functions.Services;

public class OpenAIService(ILogger<OpenAIService> logger) : IOpenAIService
{
    private readonly ChatClient _chat = new AzureOpenAIClient(
        new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
        new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")!))
        .GetChatClient(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o");

    private const string StyleReference = """
        Harry writes in first-person, direct, opinionated tone. Technical but accessible.
        He uses real-world examples, often from banking or logistics systems.
        He avoids fluff — every paragraph makes a point.
        Short sentences. Occasional rhetorical questions. No corporate speak.
        """;

    public async Task<string> SelectTopicAsync(string topicsJson, CancellationToken ct = default)
    {
        var prompt = $$"""
            You are a content strategist for Harry Zhao, a Senior Software Engineer in Melbourne
            specialising in AI-augmented systems, distributed platforms, and cloud-native services.

            Here are this week's trending AI topics from multiple sources:
            {{topicsJson}}

            Select the single most interesting and timely topic for a senior engineering audience.
            Return JSON only:
            {
              "title": "article title",
              "angles": ["angle 1", "angle 2", "angle 3"],
              "why_now": "one sentence on why this is timely",
              "sources": [{"name": "source title", "url": "https://...", "from": "source type"}]
            }
            """;

        return await CompleteAsync(prompt, ct);
    }

    public async Task<string> GenerateArticleAsync(string topicJson, CancellationToken ct = default)
    {
        var prompt = $"""
            Write a technical blog article for Harry Zhao's blog (harry-zhao-au.github.io).

            Topic details: {topicJson}

            Writing style:
            {StyleReference}

            Format: Markdown, 800-1200 words.
            Include a compelling opening, code examples where relevant (C#, Python, or TypeScript),
            and a practical takeaway. No generic "In summary..." conclusions.
            Start directly with the article content.
            """;

        return await CompleteAsync(prompt, ct);
    }

    public async Task<string> GenerateXThreadAsync(string article, CancellationToken ct = default)
    {
        var prompt = $$"""
            Convert this article into an X (Twitter) thread for Harry Zhao.

            Article:
            {{article}}

            Rules:
            - Tweet 1: compelling hook, max 280 chars, no hashtags
            - Tweets 2-7: one key insight each, max 280 chars
            - Final tweet: "Full article on my blog 👇" (blog link added separately)
            - Write in Harry's voice: direct, technical, no fluff
            - No emojis except sparingly

            Return JSON only: {"tweets": ["tweet 1", "tweet 2", ...]}
            """;

        return await CompleteAsync(prompt, ct);
    }

    public async Task<string> GenerateImagePromptAsync(string article, string topicTitle, CancellationToken ct = default)
    {
        var prompt = $"""
            You are a technical illustrator generating a DALL-E cover image for a software engineering blog.

            Article title: {topicTitle}
            Article content (first 600 chars): {article[..Math.Min(600, article.Length)]}

            Extract the CORE WORKFLOW, ARCHITECTURE, or PILLARS from this article and visualise it.
            - WORKFLOW: sequence of steps or data flow rendered as a flowchart
            - ARCHITECTURE: system components and connections rendered as a layered diagram
            - PILLARS: key principles rendered as labeled card modules side by side

            Always include the title "{topicTitle}" as bold white text at the top.

            BANNED: people, developers, glowing nodes, circuits, puzzle pieces, gears, vague "futuristic" imagery.

            Return only the DALL-E prompt, 2-3 sentences.
            """;

        return await CompleteAsync(prompt, ct);
    }

    public async Task<string> RewriteArticleAsync(string article, string feedback, CancellationToken ct = default)
    {
        var prompt = $"""
            Rewrite this blog article based on the following feedback.

            Original article:
            {article}

            Feedback: {feedback}

            Return the improved article in Markdown format only.
            """;

        return await CompleteAsync(prompt, ct);
    }

    private async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        logger.LogInformation("Calling Azure OpenAI...");
        var response = await _chat.CompleteChatAsync(
            [new UserChatMessage(prompt)],
            new ChatCompletionOptions { Temperature = 0.7f },
            ct);
        return response.Value.Content[0].Text;
    }
}
