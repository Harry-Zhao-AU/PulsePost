using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PulsePost.Functions.Functions;

public class Scheduler(ILogger<Scheduler> logger)
{
    [Function(nameof(Scheduler))]
    [ServiceBusOutput("fetch-topics", Connection = "SERVICE_BUS_CONNECTION")]
    public string Run([TimerTrigger("0 22 * * 0")] TimerInfo timer)
    {
        logger.LogInformation("Pipeline triggered at {Time}", DateTime.UtcNow);
        return "start";
    }
}
