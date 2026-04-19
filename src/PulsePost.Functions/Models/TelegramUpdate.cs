using System.Text.Json.Serialization;

namespace PulsePost.Functions.Models;

public class TelegramUpdate
{
    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }
}

public class TelegramMessage
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; set; } = new();
}

public class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}
