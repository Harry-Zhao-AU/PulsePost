namespace PulsePost.Functions.Services;

public interface IOpenAIService
{
    Task<string> SelectTopicAsync(string topicsJson, CancellationToken ct = default);
    Task<string> GenerateArticleAsync(string topicJson, CancellationToken ct = default);
    Task<string> GenerateXThreadAsync(string article, CancellationToken ct = default);
    Task<string> GenerateImagePromptAsync(string article, string topicTitle, CancellationToken ct = default);
    Task<string> RewriteArticleAsync(string article, string feedback, CancellationToken ct = default);
}
