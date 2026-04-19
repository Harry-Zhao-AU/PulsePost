using PulsePost.Functions.Models;

namespace PulsePost.Functions.Services;

public interface IDraftStorageService
{
    Task SaveAsync(ArticleDraft draft, CancellationToken ct = default);
    Task<ArticleDraft?> GetLatestPendingAsync(CancellationToken ct = default);
    Task UpdateStatusAsync(string rowKey, string status, CancellationToken ct = default);
    Task PurgeOlderThanAsync(int days, CancellationToken ct = default);
}
