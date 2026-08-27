using WardenId.Audit.Infrastructure.Sealing;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

/// <summary>Always acquires (single-process test, no real contention) unless told to deny.</summary>
public sealed class FakeChannelAdvisoryLock(bool alwaysDenies = false) : IChannelAdvisoryLock
{
    private sealed class NoopHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public Task<IAsyncDisposable?> TryAcquireAsync(AuditDbContext db, long lockKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable?>(alwaysDenies ? null : new NoopHandle());
}
