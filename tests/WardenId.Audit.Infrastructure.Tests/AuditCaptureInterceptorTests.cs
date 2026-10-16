using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Tests.Fakes;

namespace WardenId.Audit.Infrastructure.Tests;

public class AuditCaptureInterceptorTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();

    private static (TestAuditableDbContext Db, IAuditContextAccessor Accessor) CreateSut(string databaseName)
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var interceptor = new AuditCaptureInterceptor(accessor);
        return (TestAuditableDbContext.CreateNew(databaseName, interceptor), accessor);
    }

    private static IDisposable BeginAmbientScope(IAuditContextAccessor accessor, Guid? tenantId = null) =>
        accessor.BeginScope(new AuditAmbientContext(
            Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.Platform()).Value,
            tenantId,
            Correlation.Create(requestId: "req-1"),
            ClientContext.Create("203.0.113.7").Value));

    private static async Task<AuditEvent> SingleAuditEventAsync(TestAuditableDbContext db)
    {
        var record = await db.AuditEvents.SingleAsync();
        return CanonicalAuditEventSerializer.Deserialize(record.Payload!);
    }

    [Fact]
    public async Task Create_AddsAuditRow_WithEveryPropertyAsNewValue_AndNoOldValue()
    {
        var (db, accessor) = CreateSut(nameof(Create_AddsAuditRow_WithEveryPropertyAsNewValue_AndNoOldValue));
        using var scope = BeginAmbientScope(accessor);

        db.TestAggregates.Add(TestAggregate.Create("Acme", "s3cr3t"));
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var auditEvent = await SingleAuditEventAsync(db);
        Assert.Equal(AuditEventType.AdminEntityCreate, auditEvent.EventType);
        var nameChange = auditEvent.Changes.Single(c => c.PropertyName == nameof(TestAggregate.Name));
        Assert.Null(nameChange.OldValue);
        Assert.Equal("Acme", ((AuditValue.Plain)nameChange.NewValue!).DisplayValue);
    }

    [Fact]
    public async Task Create_FillsTheSearchColumnsOnTheAuditRow()
    {
        var (db, accessor) = CreateSut(nameof(Create_FillsTheSearchColumnsOnTheAuditRow));
        using var scope = BeginAmbientScope(accessor);

        var aggregate = TestAggregate.Create("Acme");
        db.TestAggregates.Add(aggregate);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var record = await db.AuditEvents.SingleAsync();
        Assert.Equal(AuditEventType.AdminEntityCreate.Key, record.EventType);
        Assert.Equal("platform:adminuser:admin-1", record.Actor);
        Assert.Equal("admin-1", record.ActorId);
        Assert.Equal([$"tenant:{aggregate.Id}"], record.Targets);
        Assert.Equal(nameof(AuditResult.Success), record.Outcome);
    }

    [Fact]
    public async Task Update_DiffIncludesTheChangedProperty_WithBothOldAndNewValues()
    {
        var (db, accessor) = CreateSut(nameof(Update_DiffIncludesTheChangedProperty_WithBothOldAndNewValues));
        using var scope = BeginAmbientScope(accessor);

        var aggregate = TestAggregate.Create("Acme");
        db.TestAggregates.Add(aggregate);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        aggregate.Rename("Acme Corp");
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var updateEvent = CanonicalAuditEventSerializer.Deserialize(
            (await db.AuditEvents.OrderBy(e => e.OccurredAt).ToListAsync()).Last().Payload!);

        Assert.Equal(AuditEventType.AdminEntityUpdate, updateEvent.EventType);
        var nameChange = updateEvent.Changes.Single(c => c.PropertyName == nameof(TestAggregate.Name));
        Assert.Equal("Acme", ((AuditValue.Plain)nameChange.OldValue!).DisplayValue);
        Assert.Equal("Acme Corp", ((AuditValue.Plain)nameChange.NewValue!).DisplayValue);
    }

    [Fact]
    public async Task SensitivePropertyChange_IsRedacted_NeverPlainText()
    {
        var (db, accessor) = CreateSut(nameof(SensitivePropertyChange_IsRedacted_NeverPlainText));
        using var scope = BeginAmbientScope(accessor);

        var aggregate = TestAggregate.Create("Acme", "old-secret");
        db.TestAggregates.Add(aggregate);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        aggregate.SetSecret("new-secret");
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var updateEvent = CanonicalAuditEventSerializer.Deserialize(
            (await db.AuditEvents.OrderBy(e => e.OccurredAt).ToListAsync()).Last().Payload!);

        var secretChange = updateEvent.Changes.Single(c => c.PropertyName == nameof(TestAggregate.Secret));
        var redacted = Assert.IsType<AuditValue.Redacted>(secretChange.NewValue);
        Assert.Equal(ProtectedValue.Redact("new-secret").Hash, redacted.Value.Hash);
    }

    [Fact]
    public async Task SoftDelete_ProducesDeleteEventType()
    {
        var (db, accessor) = CreateSut(nameof(SoftDelete_ProducesDeleteEventType));
        using var scope = BeginAmbientScope(accessor);

        var aggregate = TestAggregate.Create("Acme");
        db.TestAggregates.Add(aggregate);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        db.TestAggregates.Remove(aggregate);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var deleteEvent = CanonicalAuditEventSerializer.Deserialize(
            (await db.AuditEvents.OrderBy(e => e.OccurredAt).ToListAsync()).Last().Payload!);

        Assert.Equal(AuditEventType.AdminEntityDelete, deleteEvent.EventType);
        Assert.True(aggregate.IsDeleted);
    }

    [Fact]
    public async Task MissingAmbientContext_Throws_RatherThanSavingUnaudited()
    {
        var (db, _) = CreateSut(nameof(MissingAmbientContext_Throws_RatherThanSavingUnaudited));

        db.TestAggregates.Add(TestAggregate.Create("Acme"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync(ActorUserId, CancellationToken.None));
    }

    [Fact]
    public async Task UnregisteredAuditTargetTypeKey_Throws_RatherThanSavingUnaudited()
    {
        var (db, accessor) = CreateSut(nameof(UnregisteredAuditTargetTypeKey_Throws_RatherThanSavingUnaudited));
        using var scope = BeginAmbientScope(accessor);

        db.BadTargetTypeAggregates.Add(BadTargetTypeAggregate.Create("Acme"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync(ActorUserId, CancellationToken.None));
    }

    [Fact]
    public async Task AuditRowAndBusinessRow_CommitTogether_InOneSaveChangesCall()
    {
        var (db, accessor) = CreateSut(nameof(AuditRowAndBusinessRow_CommitTogether_InOneSaveChangesCall));
        using var scope = BeginAmbientScope(accessor);

        db.TestAggregates.Add(TestAggregate.Create("Acme"));
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        Assert.Equal(1, await db.TestAggregates.CountAsync());
        Assert.Equal(1, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task TenantId_OnStoredRow_ComesFromAmbientContext_NotFromActor()
    {
        var (db, accessor) = CreateSut(nameof(TenantId_OnStoredRow_ComesFromAmbientContext_NotFromActor));
        var tenantId = Guid.NewGuid();
        using var scope = BeginAmbientScope(accessor, tenantId);

        db.TestAggregates.Add(TestAggregate.Create("Acme"));
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var record = await db.AuditEvents.SingleAsync();
        Assert.Equal(tenantId, record.TenantId);
    }
}
