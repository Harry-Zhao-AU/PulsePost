using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PulsePost.Functions.Services;

namespace PulsePost.Functions.Functions;

public class FetchTopics(ITopicFetchService topicFetchService, ILogger<FetchTopics> logger)
{
    [Function(nameof(FetchTopics))]
    [ServiceBusOutput("generate-article", Connection = "SERVICE_BUS_CONNECTION")]
    public async Task<string> Run(
        [ServiceBusTrigger("fetch-topics", Connection = "SERVICE_BUS_CONNECTION")] string message,
        CancellationToken ct)
    {
        logger.LogInformation("Fetching topics...");
        var topics = await topicFetchService.FetchAllTopicsAsync(ct);
        logger.LogInformation("Topics fetched successfully");
        return topics;
    }
}
