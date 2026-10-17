using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Domain;
using WardenId.Identity.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>A tenant's identity policy overrides, saved through the real Identity context, land in admin.config, diffed.</summary>
[Collection(PostgresCollection.Name)]
public sealed class IdentityPolicyCapturePostgresTests(PostgresFixture postgres)
{
    private static readonly Guid AdminId = Guid.Parse("0198a000-0000-7000-8000-000000000002");

    private readonly AsyncLocalAuditContextAccessor _accessor = new();

    private IdentityDbContext Identity(string connectionString) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .AddInterceptors(new AuditCaptureInterceptor(_accessor))
            .Options);

    [SkippableFact]
    public async Task PolicyChanges_AreAuditedInAdminConfig_TargetedByTenant()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var audit = PostgresFixture.CreateContext(connectionString)) await audit.Database.MigrateAsync();
        await using (var identity = Identity(connectionString)) await identity.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        using var scope = _accessor.BeginScope(new AuditAmbientContext(
            Actor.Create(ActorType.AuthenticatedUnknown, AdminId.ToString(), ActorScope.ForTenant(tenantId)).Value,
            tenantId,
            Correlation.Create(requestId: "req-1"),
            ClientContext.Unknown()));

        await using (var db = Identity(connectionString))
        {
            db.TenantPolicies.Add(IdentityTenantPolicy.Create(tenantId, IdentityPolicyOverrides.None with { PasswordMinLength = 12 }).Value);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using (var db = Identity(connectionString))
        {
            var policy = await db.TenantPolicies.SingleAsync(p => p.TenantId == tenantId);
            Assert.True(policy.Replace(IdentityPolicyOverrides.None with { MfaMode = MfaPolicyMode.Required }).IsSuccess);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using var auditDb = PostgresFixture.CreateContext(connectionString);
        var events = (await auditDb.AuditEvents.OrderBy(e => e.OccurredAt).ToListAsync())
            .Select(r => (Record: r, Event: CanonicalAuditEventSerializer.Deserialize(r.Payload!)))
            .ToList();

        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(nameof(AuditChannel.AdminConfig), e.Record.Channel);
            Assert.Equal([$"identity_policy:{tenantId}"], e.Record.Targets);
            Assert.Equal(AdminId.ToString(), e.Record.ActorId);
        });

        Assert.Equal(AuditEventType.AdminEntityCreate, events[0].Event.EventType);
        var updated = events[1].Event;
        Assert.Equal(AuditEventType.AdminEntityUpdate, updated.EventType);
        var minLength = updated.Changes.Single(c => c.PropertyName == nameof(IdentityTenantPolicy.PasswordMinLength));
        Assert.Equal("12", (minLength.OldValue as AuditValue.Plain)?.DisplayValue);
        Assert.Equal("null", (minLength.NewValue as AuditValue.Plain)?.DisplayValue);
        var mfa = updated.Changes.Single(c => c.PropertyName == nameof(IdentityTenantPolicy.MfaMode));
        Assert.Equal("Required", (mfa.NewValue as AuditValue.Plain)?.DisplayValue);
    }
}
