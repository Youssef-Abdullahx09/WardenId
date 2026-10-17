using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Tests.Fakes;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>Children an aggregate opts in through AuditedNavigationNames are diffed into its own event.</summary>
public class AuditCaptureInterceptorChildrenTests
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
            ClientContext.Unknown()));
        return (db, scope);
    }

    private static async Task<List<AuditEvent>> EventsAsync(TestAuditableDbContext db) =>
        (await db.AuditEvents.OrderBy(e => e.OccurredAt).ToListAsync())
        .Select(r => CanonicalAuditEventSerializer.Deserialize(r.Payload!))
        .ToList();

    private static string? Text(AuditValue? value) => (value as AuditValue.Plain)?.DisplayValue;

    [Fact]
    public async Task Create_IncludesAddedChildren_KeyedByTheirId()
    {
        var (db, scope) = CreateSut(nameof(Create_IncludesAddedChildren_KeyedByTheirId));
        using var _ = scope;

        var parent = ParentAggregate.Create("Acme");
        var key = parent.AddKey("primary", "raw-material");
        parent.AddTag("blue");
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var created = Assert.Single(await EventsAsync(db));
        Assert.Equal(AuditEventType.AdminEntityCreate, created.EventType);
        var keyName = created.Changes.Single(c => c.PropertyName == $"Keys[{key.Id}].Name");
        Assert.Null(keyName.OldValue);
        Assert.Equal("primary", Text(keyName.NewValue));
        Assert.Equal("blue", Text(created.Changes.Single(c => c.PropertyName == "Tags[blue].Value").NewValue));
    }

    [Fact]
    public async Task SensitiveChildProperty_IsRedacted()
    {
        var (db, scope) = CreateSut(nameof(SensitiveChildProperty_IsRedacted));
        using var _ = scope;

        var parent = ParentAggregate.Create("Acme");
        var key = parent.AddKey("primary", "raw-material");
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var created = Assert.Single(await EventsAsync(db));
        var material = created.Changes.Single(c => c.PropertyName == $"Keys[{key.Id}].Material");
        var redacted = Assert.IsType<AuditValue.Redacted>(material.NewValue);
        Assert.Equal(ProtectedValue.Redact("raw-material").Hash, redacted.Value.Hash);
        Assert.DoesNotContain(created.Changes, c => Text(c.NewValue) == "raw-material" || Text(c.OldValue) == "raw-material");
    }

    [Fact]
    public async Task ChildOnlyChange_ProducesAnUpdateEvent_ForTheAggregate()
    {
        var (db, scope) = CreateSut(nameof(ChildOnlyChange_ProducesAnUpdateEvent_ForTheAggregate));
        using var _ = scope;

        var parent = ParentAggregate.Create("Acme");
        var oldKey = parent.AddKey("primary", "m1");
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        oldKey.Deactivate();
        var newKey = parent.AddKey("next", "m2");
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var update = (await EventsAsync(db)).Last();
        Assert.Equal(AuditEventType.AdminEntityUpdate, update.EventType);
        Assert.Equal("public-1", update.Targets.Single().Id);
        var deactivated = update.Changes.Single(c => c.PropertyName == $"Keys[{oldKey.Id}].Active");
        Assert.Equal("True", Text(deactivated.OldValue));
        Assert.Equal("False", Text(deactivated.NewValue));
        Assert.Contains(update.Changes, c => c.PropertyName == $"Keys[{newKey.Id}].Name" && Text(c.NewValue) == "next");
        Assert.DoesNotContain(update.Changes, c => c.PropertyName == nameof(ParentAggregate.Name));
    }

    [Fact]
    public async Task RemovedOwnedChild_IsRecordedWithItsOldValue()
    {
        var (db, scope) = CreateSut(nameof(RemovedOwnedChild_IsRecordedWithItsOldValue));
        using var _ = scope;

        var parent = ParentAggregate.Create("Acme");
        parent.AddTag("blue");
        parent.AddTag("green");
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        parent.RemoveTag("blue");
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var update = (await EventsAsync(db)).Last();
        var removed = Assert.Single(update.Changes);
        Assert.Equal("Tags[blue].Value", removed.PropertyName);
        Assert.Equal("blue", Text(removed.OldValue));
        Assert.Null(removed.NewValue);
    }

    [Fact]
    public async Task ReplacedOwnedReference_IsOneChangePerProperty()
    {
        var (db, scope) = CreateSut(nameof(ReplacedOwnedReference_IsOneChangePerProperty));
        using var _ = scope;

        var parent = ParentAggregate.Create("Acme");
        parent.SetLimits(10);
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        parent.SetLimits(20);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var update = (await EventsAsync(db)).Last();
        var max = Assert.Single(update.Changes);
        Assert.Equal("Limits.Max", max.PropertyName);
        Assert.Equal("10", Text(max.OldValue));
        Assert.Equal("20", Text(max.NewValue));
    }

    [Fact]
    public async Task NavigationNotOptedIn_IsNeitherDiffedNorAnEvent()
    {
        var (db, scope) = CreateSut(nameof(NavigationNotOptedIn_IsNeitherDiffedNorAnEvent));
        using var _ = scope;

        var parent = ParentAggregate.Create("Acme");
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        parent.AddNote("private");
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var created = Assert.Single(await EventsAsync(db));
        Assert.DoesNotContain(created.Changes, c => c.PropertyName.StartsWith("Notes"));
    }

    [Fact]
    public async Task UnchangedAggregate_WritesNoEvent()
    {
        var (db, scope) = CreateSut(nameof(UnchangedAggregate_WritesNoEvent));
        using var _ = scope;

        db.ParentAggregates.Add(ParentAggregate.Create("Acme"));
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        Assert.Single(await EventsAsync(db));
    }

    [Fact]
    public async Task AuditTargetId_ReplacesTheEntityIdOnTheTarget()
    {
        var (db, scope) = CreateSut(nameof(AuditTargetId_ReplacesTheEntityIdOnTheTarget));
        using var _ = scope;

        db.ParentAggregates.Add(ParentAggregate.Create("Acme", publicName: "client-abc"));
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var record = await db.AuditEvents.SingleAsync();
        Assert.Equal(["oauth_client:client-abc"], record.Targets);
        Assert.Equal(nameof(AuditChannel.AdminConfig), record.Channel);
    }

    [Fact]
    public async Task CollectionProperty_IsRenderedAsItsItems()
    {
        var (db, scope) = CreateSut(nameof(CollectionProperty_IsRenderedAsItsItems));
        using var _ = scope;

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var parent = ParentAggregate.Create("Acme");
        parent.SetScopeIds([first, second]);
        db.ParentAggregates.Add(parent);
        await db.SaveChangesAsync(ActorUserId, CancellationToken.None);

        var created = Assert.Single(await EventsAsync(db));
        Assert.Equal($"[{first}, {second}]", Text(created.Changes.Single(c => c.PropertyName == nameof(ParentAggregate.ScopeIds)).NewValue));
    }
}
