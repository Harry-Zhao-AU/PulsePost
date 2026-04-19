using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using PulsePost.Functions.Models;

namespace PulsePost.Functions.Services;

public class DraftStorageService(ILogger<DraftStorageService> logger) : IDraftStorageService
{
    private readonly TableClient _table = new(
        Environment.GetEnvironmentVariable("STORAGE_CONNECTION"),
        "ArticleDrafts");

    public async Task SaveAsync(ArticleDraft draft, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct);
        await _table.AddEntityAsync(draft, ct);
        logger.LogInformation("Draft saved: {RowKey}", draft.RowKey);
    }

    public async Task<ArticleDraft?> GetLatestPendingAsync(CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct);
        return _table
            .Query<ArticleDraft>(d => d.PartitionKey == "draft" && d.Status == "pending", cancellationToken: ct)
            .OrderByDescending(d => d.RowKey)
            .FirstOrDefault();
    }

    public async Task UpdateStatusAsync(string rowKey, string status, CancellationToken ct = default)
    {
        var entity = await _table.GetEntityAsync<ArticleDraft>("draft", rowKey, cancellationToken: ct);
        entity.Value.Status = status;
        await _table.UpdateEntityAsync(entity.Value, entity.Value.ETag, TableUpdateMode.Replace, ct);
    }

    public async Task PurgeOlderThanAsync(int days, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct);
        var cutoff = DateTime.UtcNow.AddDays(-days).ToString("yyyyMMddHHmmss");
        var old = _table.Query<ArticleDraft>(d => d.PartitionKey == "draft" && d.RowKey.CompareTo(cutoff) < 0, cancellationToken: ct);

        foreach (var draft in old)
        {
            await _table.DeleteEntityAsync(draft.PartitionKey, draft.RowKey, cancellationToken: ct);
            logger.LogInformation("Purged draft: {RowKey}", draft.RowKey);
        }
    }
}
