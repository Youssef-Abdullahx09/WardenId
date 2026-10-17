using Microsoft.EntityFrameworkCore;
using Npgsql;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.Audit.Application;
using WardenId.Protocol.Domain.TokenPolicies;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Protocol.Infrastructure.Tests.Persistence;

/// <summary>
/// A tenant's token-lifetime policy (Protocol schema) migrates on Postgres in the documented order (OpenIddict
/// first) and every change lands in admin.config against the tenant, diffed, in the same save. Needs a Postgres
/// server in <see cref="ServerVariable"/> (a throwaway database is created and dropped); skipped otherwise.
/// </summary>
public sealed class TenantTokenPolicyPostgresTests
{
    private const string ServerVariable = "WARDENID_TEST_POSTGRES";

    private static readonly Guid AdminId = Guid.Parse("0198a000-0000-7000-8000-000000000002");

    private readonly AsyncLocalAuditContextAccessor _accessor = new();

    private ProtocolDbContext Protocol(string connectionString) =>
        new(new DbContextOptionsBuilder<ProtocolDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ProtocolDbContext.Schema))
            .AddInterceptors(new AuditCaptureInterceptor(_accessor))
            .Options);

    private static OpenIddictDbContext OpenIddict(string connectionString) =>
        new(new DbContextOptionsBuilder<OpenIddictDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OpenIddictDbContext.Schema))
            .UseOpenIddict()
            .Options);

    private static string? Text(AuditValue? value) => (value as AuditValue.Plain)?.DisplayValue;

    [SkippableFact]
    public async Task TenantTokenPolicy_CreateAndReplace_AreAuditedInAdminConfig_AgainstTheTenant()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        Skip.If(string.IsNullOrWhiteSpace(server), $"Needs Postgres: set {ServerVariable} to a server connection string.");
        var database = $"wardenid_protocol_tests_{Guid.NewGuid():N}";
        await ExecuteAsync(server!, $"CREATE DATABASE \"{database}\"");
        try
        {
            await RunAsync(new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(server!, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
        }
    }

    private static AuditDbContext Audit(string connectionString) =>
        new(new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
            .Options);

    private static async Task ExecuteAsync(string server, string sql)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RunAsync(string connectionString)
    {
        await using (var audit = Audit(connectionString)) await audit.Database.MigrateAsync();
        await using (var openIddict = OpenIddict(connectionString)) await openIddict.Database.MigrateAsync();
        await using (var protocol = Protocol(connectionString)) await protocol.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        using var scope = _accessor.BeginScope(new AuditAmbientContext(
            Actor.Create(ActorType.AuthenticatedUnknown, AdminId.ToString(), ActorScope.ForTenant(tenantId)).Value,
            tenantId,
            Correlation.Create(requestId: "req-1"),
            ClientContext.Unknown()));

        await using (var db = Protocol(connectionString))
        {
            db.TenantTokenPolicies.Add(TenantTokenPolicy.Create(tenantId, TenantLifetimeOverrides.Create(
                TimeSpan.FromMinutes(5), null, null, null, null, null, null).Value));
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using (var db = Protocol(connectionString))
        {
            var policy = await db.TenantTokenPolicies.SingleAsync(p => p.TenantId == tenantId);
            Assert.Equal(TimeSpan.FromMinutes(5), policy.AccessTokenLifetime);
            policy.Replace(TenantLifetimeOverrides.Create(null, null, null, null, null, TimeSpan.FromHours(1), null).Value);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using var auditDb = Audit(connectionString);
        var events = (await auditDb.AuditEvents.OrderBy(e => e.OccurredAt).ToListAsync())
            .Select(r => (Record: r, Event: CanonicalAuditEventSerializer.Deserialize(r.Payload!)))
            .ToList();
        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(nameof(AuditChannel.AdminConfig), e.Record.Channel);
            Assert.Equal([$"tenant:{tenantId}"], e.Record.Targets);
            Assert.Equal(AdminId.ToString(), e.Record.ActorId);
        });

        var created = events[0].Event;
        Assert.Equal(AuditEventType.AdminEntityCreate, created.EventType);
        Assert.NotNull(Text(created.Changes.Single(c => c.PropertyName == nameof(TenantTokenPolicy.AccessTokenLifetime)).NewValue));

        var replaced = events[1].Event;
        Assert.Equal(AuditEventType.AdminEntityUpdate, replaced.EventType);
        var cleared = replaced.Changes.Single(c => c.PropertyName == nameof(TenantTokenPolicy.AccessTokenLifetime));
        Assert.NotNull(Text(cleared.OldValue));
        Assert.Contains(replaced.Changes, c => c.PropertyName == nameof(TenantTokenPolicy.SessionCookieLifetime));
    }
}
