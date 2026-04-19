namespace PulsePost.Functions.Services;

public interface ITelegramService
{
    Task SendMessageAsync(string message, CancellationToken ct = default);
}
