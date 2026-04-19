namespace PulsePost.Functions.Services;

public interface IPublishService
{
    Task CreateBlogPrAsync(string title, string article, string imagePrompt, CancellationToken ct = default);
}
