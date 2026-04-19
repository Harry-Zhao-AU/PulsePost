using Azure;
using Azure.Data.Tables;

namespace PulsePost.Functions.Models;

public class ArticleDraft : ITableEntity
{
    public string PartitionKey { get; set; } = "draft";
    public string RowKey { get; set; } = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Topic { get; set; } = string.Empty;
    public string Article { get; set; } = string.Empty;
    public string XThread { get; set; } = string.Empty;
    public string ImagePrompt { get; set; } = string.Empty;
    public string Sources { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
