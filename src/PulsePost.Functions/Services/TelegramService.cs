using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PulsePost.Functions.Services;

public class TelegramService(HttpClient httpClient, ILogger<TelegramService> logger) : ITelegramService
{
    private readonly string _token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")!;
    private readonly string _chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")!;

    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        var payload = new { chat_id = _chatId, text = message, parse_mode = "Markdown", disable_web_page_preview = true };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(
            $"https://api.telegram.org/bot{_token}/sendMessage", content, ct);

        if (!response.IsSuccessStatusCode)
            logger.LogError("Telegram send failed: {Status}", response.StatusCode);
    }
}
