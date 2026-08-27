using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// One active sealer per (channel, tenant) across all replicas (spec §6). Returns null when
/// another replica already holds the lock for this key — the caller should skip this pass,
/// not wait. Dispose releases the lock; a killed process releases it anyway on connection
/// drop, which is the whole point of an advisory lock over an app-level mutex.
/// </summary>
public interface IChannelAdvisoryLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(AuditDbContext db, long lockKey, CancellationToken cancellationToken = default);
}
