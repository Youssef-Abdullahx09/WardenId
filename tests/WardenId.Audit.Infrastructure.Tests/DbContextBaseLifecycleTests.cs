using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Tests.Fakes;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>What DbContextBase does to an entity's row on insert and delete, seen through a fresh context like a later request.</summary>
public class DbContextBaseLifecycleTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();

    private static (TestAuditableDbContext Db, IDisposable Scope) CreateSut(string databaseName)
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var db = TestAuditableDbContext.CreateNew(databaseName, new AuditCaptureInterceptor(accessor));
        var scope = accessor.BeginScope(new AuditAmbientContext(
            Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.Platform()).Value,
            null,
            Correlation.Create(requestId: "req-1"),
            ClientContext.Create("203.0.113.7").Value));
        return (db, scope);
    }

    [Fact]
    public async Task AddingACreationTrackedEntity_StampsCreatedAt()
    {
        var (db, scope) = CreateSut(nameof(AddingACreationTrackedEntity_StampsCreatedAt));
        using var _ = scope;

        var aggregate = TestAggregate.Create("Acme");
        db.TestAggregates.Add(aggregate);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        Assert.InRange(aggregate.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RemovingAChildEntity_DeletesItsRow()
    {
        var databaseName = nameof(RemovingAChildEntity_DeletesItsRow);
        var (db, scope) = CreateSut(databaseName);
        using var _ = scope;

        var parent = ParentAggregate.Create("Acme");
        var key = parent.AddKey("primary", "m1");
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        parent.RemoveKey(key.Id);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var (fresh, freshScope) = CreateSut(databaseName);
        using var __ = freshScope;
        Assert.False(await fresh.Set<ChildKey>().AnyAsync(k => k.Id == key.Id));
    }

    [Fact]
    public async Task RemovingASoftDeletableEntity_Throws()
    {
        var (db, scope) = CreateSut(nameof(RemovingASoftDeletableEntity_Throws));
        using var _ = scope;

        var aggregate = TestAggregate.Create("Acme");
        db.TestAggregates.Add(aggregate);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        db.TestAggregates.Remove(aggregate);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ActorUserId, CancellationToken.None));
    }
}
