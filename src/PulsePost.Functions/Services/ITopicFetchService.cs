namespace PulsePost.Functions.Services;

public interface ITopicFetchService
{
    Task<string> FetchAllTopicsAsync(CancellationToken ct = default);
}
