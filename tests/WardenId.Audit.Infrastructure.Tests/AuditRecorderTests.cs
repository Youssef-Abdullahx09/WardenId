using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace WardenId.Audit.Infrastructure.Tests;

public class AuditRecorderTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    private sealed class ThrowingAuditDbContextFactory : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() => throw new InvalidOperationException("simulated DB failure");
    }

    private static AuditEvent MakeEvent() => AuditEvent.Create(
        Guid.NewGuid(), AuditChannel.AdminConfig, AuditEventType.AdminTenantSuspend, 1, DateTimeOffset.UtcNow,
        AuditSeverity.Warning, Actor.Anonymous(ActorScope.Platform()), AuditOutcome.Success(),
        ClientContext.Unknown(), AuthContext.None(), Correlation.None()).Value;

    [Fact]
    public async Task RecordAsync_PersistsPayloadAndMatchingLeafHash()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RecordAsync_PersistsPayloadAndMatchingLeafHash));
        var recorder = new AuditRecorder(factory, NullLogger<AuditRecorder>.Instance);
        var auditEvent = MakeEvent();
        var tenantId = Guid.NewGuid();

        await recorder.RecordAsync(auditEvent, tenantId);

        await using var db = factory.CreateDbContext();
        var stored = await db.AuditEvents.SingleAsync();

        Assert.Equal(auditEvent.Id, stored.Id);
        Assert.Equal(auditEvent.Channel.ToString(), stored.Channel);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.Null(stored.Sequence);
        Assert.NotNull(stored.Payload);
        Assert.Equal(MerkleHash.Leaf(stored.Payload!), stored.LeafHash);
        Assert.Equal(CanonicalAuditEventSerializer.Serialize(auditEvent), stored.Payload);
        Assert.Equal(auditEvent.EventType.Key, stored.EventType);
        Assert.Equal(auditEvent.Actor.ToSerializedForm(), stored.Actor);
        Assert.Equal(nameof(AuditResult.Success), stored.Outcome);
    }

    [Fact]
    public async Task RecordAsync_WithNullTenantId_PersistsPlatformScopedRow()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RecordAsync_WithNullTenantId_PersistsPlatformScopedRow));
        var recorder = new AuditRecorder(factory, NullLogger<AuditRecorder>.Instance);

        await recorder.RecordAsync(MakeEvent(), tenantId: null);

        await using var db = factory.CreateDbContext();
        var stored = await db.AuditEvents.SingleAsync();

        Assert.Null(stored.TenantId);
    }

    [Fact]
    public async Task RecordAsync_FailsOpen_WhenTheStoreIsUnavailable()
    {
        // I10 — a write failure here must never surface to the caller.
        var recorder = new AuditRecorder(new ThrowingAuditDbContextFactory(), NullLogger<AuditRecorder>.Instance);

        await recorder.RecordAsync(MakeEvent(), tenantId: null);
        // No assertion beyond "did not throw" — that not throwing is the entire contract.
    }
}
