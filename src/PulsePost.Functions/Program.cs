using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PulsePost.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpClient();
        services.AddSingleton<IOpenAIService, OpenAIService>();
        services.AddSingleton<ITelegramService, TelegramService>();
        services.AddSingleton<ITopicFetchService, TopicFetchService>();
        services.AddSingleton<IDraftStorageService, DraftStorageService>();
        services.AddSingleton<IPublishService, PublishService>();
    })
    .Build();

host.Run();
