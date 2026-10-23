using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Infrastructure.Persistence;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Infrastructure.Persistence;
using ClientId = WardenId.ClientManagement.Domain.ClientId;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>
/// Tenant and client writes through the real Tenancy and ClientManagement contexts (their mappings,
/// owned types and soft delete) land in admin.config, diffed, with key material and secret hashes
/// redacted, in the same save as the change.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AdminConfigCapturePostgresTests(PostgresFixture postgres)
{
    private static readonly Guid AdminId = Guid.Parse("0198a000-0000-7000-8000-000000000001");
    private const string PrivateKeyText = "private-key-bytes";
    private const string SecretHash = "5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8";

    private readonly AsyncLocalAuditContextAccessor _accessor = new();

    private IDisposable BeginAdminScope(Guid tenantId) =>
        _accessor.BeginScope(new AuditAmbientContext(
            Actor.Create(ActorType.AuthenticatedUnknown, AdminId.ToString(), ActorScope.ForTenant(tenantId)).Value,
            tenantId,
            Correlation.Create(requestId: "req-1"),
            ClientContext.Unknown()));

    private TContext Create<TContext>(string connectionString, string schema) where TContext : DbContext =>
        (TContext)Activator.CreateInstance(
            typeof(TContext),
            new DbContextOptionsBuilder<TContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
                .AddInterceptors(new AuditCaptureInterceptor(_accessor))
                .Options)!;

    private TenancyDbContext Tenancy(string connectionString) => Create<TenancyDbContext>(connectionString, TenancyDbContext.Schema);

    private ClientManagementDbContext Clients(string connectionString) =>
        Create<ClientManagementDbContext>(connectionString, ClientManagementDbContext.Schema);

    private async Task<string> MigratedDatabaseAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var audit = PostgresFixture.CreateContext(connectionString)) await audit.Database.MigrateAsync();
        await using (var tenancy = Tenancy(connectionString)) await tenancy.Database.MigrateAsync();
        await using (var clients = Clients(connectionString)) await clients.Database.MigrateAsync();
        return connectionString;
    }

    private static async Task<List<(AuditEventRecord Record, AuditEvent Event)>> EventsAsync(string connectionString)
    {
        await using var db = PostgresFixture.CreateContext(connectionString);
        return (await db.AuditEvents.OrderBy(e => e.OccurredAt).ToListAsync())
            .Select(r => (r, CanonicalAuditEventSerializer.Deserialize(r.Payload!)))
            .ToList();
    }

    private static string? Text(AuditValue? value) => (value as AuditValue.Plain)?.DisplayValue;

    private static PropertyChange Change(AuditEvent auditEvent, string name) =>
        auditEvent.Changes.Single(c => c.PropertyName == name);

    private static void AssertNoPlainText(AuditEvent auditEvent, string secret) =>
        Assert.DoesNotContain(auditEvent.Changes, c =>
            (Text(c.OldValue)?.Contains(secret) ?? false) || (Text(c.NewValue)?.Contains(secret) ?? false));

    private static SigningKey NewSigningKey(Guid kid) =>
        SigningKey.CreateEs256(kid, $$"""{"kty":"EC","kid":"{{kid:N}}"}""", System.Text.Encoding.UTF8.GetBytes(PrivateKeyText));

    [SkippableFact]
    public async Task TenantLifecycle_IsAuditedInAdminConfig_WithKeyMaterialRedacted()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await MigratedDatabaseAsync();

        var tenant = Tenant.Create(Slug.Create("acme").Value, "Acme").Value;
        var firstKid = Guid.NewGuid();
        tenant.AssignInitialSigningKey(NewSigningKey(firstKid));
        tenant.Activate();
        using var scope = BeginAdminScope(tenant.Id);

        await using (var db = Tenancy(connectionString))
        {
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        var secondKid = Guid.NewGuid();
        await using (var db = Tenancy(connectionString))
        {
            var loaded = await db.Tenants.Include(t => t.SigningKeys).SingleAsync();
            Assert.True(loaded.RotateSigningKey(NewSigningKey(secondKid)).IsSuccess);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using (var db = Tenancy(connectionString))
        {
            var loaded = await db.Tenants.Include(t => t.SigningKeys).SingleAsync();
            Assert.True(loaded.Archive().IsSuccess);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        var events = await EventsAsync(connectionString);
        Assert.Equal(3, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(nameof(AuditChannel.AdminConfig), e.Record.Channel);
            Assert.Equal(tenant.Id, e.Record.TenantId);
            Assert.Equal([$"tenant:{tenant.Id}"], e.Record.Targets);
            Assert.Equal(AdminId.ToString(), e.Record.ActorId);
            AssertNoPlainText(e.Event, PrivateKeyText);
            AssertNoPlainText(e.Event, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(PrivateKeyText)));
            AssertNoPlainText(e.Event, "\"kty\"");
        });

        var (_, created) = events[0];
        Assert.Equal(AuditEventType.AdminEntityCreate, created.EventType);
        Assert.Equal("acme", Text(Change(created, nameof(Tenant.Slug)).NewValue));
        var firstKeyId = tenant.SigningKeys.Single().Id;
        Assert.Equal(firstKid.ToString(), Text(Change(created, $"SigningKeys[{firstKeyId}].SigningKeyId").NewValue));
        Assert.IsType<AuditValue.Redacted>(Change(created, $"SigningKeys[{firstKeyId}].EncryptedPrivateKey").NewValue);
        Assert.IsType<AuditValue.Redacted>(Change(created, $"SigningKeys[{firstKeyId}].PublicKeyJwk").NewValue);

        var (_, rotated) = events[1];
        Assert.Equal(AuditEventType.AdminEntityUpdate, rotated.EventType);
        var retiring = Change(rotated, $"SigningKeys[{firstKeyId}].Status");
        Assert.Equal(nameof(SigningKeyStatus.Active), Text(retiring.OldValue));
        Assert.Equal(nameof(SigningKeyStatus.Retiring), Text(retiring.NewValue));
        Assert.Contains(rotated.Changes, c => c.PropertyName.EndsWith(".SigningKeyId") && Text(c.NewValue) == secondKid.ToString());

        var (_, archived) = events[2];
        var status = Change(archived, nameof(Tenant.Status));
        Assert.Equal(nameof(Status.Active), Text(status.OldValue));
        Assert.Equal(nameof(Status.Archived), Text(status.NewValue));
    }

    [SkippableFact]
    public async Task ClientLifecycle_IsAuditedAgainstTheClientId_WithSecretHashesRedacted()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await MigratedDatabaseAsync();

        var tenantId = Guid.NewGuid();
        var client = Client.Create(
            tenantId, ClientType.Confidential, "Portal", GrantType.AuthorizationCode | GrantType.RefreshToken,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true,
            clientId: ClientId.Create("portal-client-0001").Value).Value;
        client.AddRedirectUri(RedirectUri.Create("https://portal.example/cb").Value);
        client.AddCorsOrigin(CorsOrigin.Create("https://portal.example").Value);
        client.AddSecret(ClientSecret.Create(SecretHash, "initial", expiresAt: null));
        using var scope = BeginAdminScope(tenantId);

        await using (var db = Clients(connectionString))
        {
            db.Clients.Add(client);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using (var db = Clients(connectionString))
        {
            var loaded = await db.Clients.Include(c => c.Secrets).SingleAsync();
            loaded.ReplaceRedirectUris([RedirectUri.Create("https://portal.example/callback").Value]);
            loaded.UpdateTokenLifetimeOverrides(TokenLifetimeOverrides.Create(300, null, null).Value);
            loaded.UpdateDisplayName("Customer portal");
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using (var db = Clients(connectionString))
        {
            var loaded = await db.Clients.Include(c => c.Secrets).SingleAsync();
            loaded.Delete();
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        var events = await EventsAsync(connectionString);
        Assert.Equal(3, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(nameof(AuditChannel.AdminConfig), e.Record.Channel);
            Assert.Equal(["oauth_client:portal-client-0001"], e.Record.Targets);
            Assert.Equal(AdminId.ToString(), e.Record.ActorId);
            AssertNoPlainText(e.Event, SecretHash);
        });

        var (_, created) = events[0];
        Assert.Equal(AuditEventType.AdminEntityCreate, created.EventType);
        var secretId = client.Secrets.Single().Id;
        Assert.IsType<AuditValue.Redacted>(Change(created, $"Secrets[{secretId}].HashedValue").NewValue);
        Assert.Equal("initial", Text(Change(created, $"Secrets[{secretId}].Label").NewValue));
        Assert.Equal("https://portal.example/cb", Text(Change(created, "RedirectUris[https://portal.example/cb].Value").NewValue));
        Assert.Equal("https://portal.example", Text(Change(created, "CorsOrigins[https://portal.example].Value").NewValue));

        var (_, updated) = events[1];
        Assert.Equal(AuditEventType.AdminEntityUpdate, updated.EventType);
        Assert.Equal("Customer portal", Text(Change(updated, nameof(Client.DisplayName)).NewValue));
        var removedUri = Change(updated, "RedirectUris[https://portal.example/cb].Value");
        Assert.Equal("https://portal.example/cb", Text(removedUri.OldValue));
        Assert.Null(removedUri.NewValue);
        Assert.Null(Change(updated, "RedirectUris[https://portal.example/callback].Value").OldValue);
        Assert.Equal("300", Text(Change(updated, "TokenLifetimeOverrides.AccessTokenSeconds").NewValue));

        var (_, deleted) = events[2];
        Assert.Equal(AuditEventType.AdminEntityDelete, deleted.EventType);
    }

    [SkippableFact]
    public async Task ClientDelete_DeletesItsSecretAndSigningKeyRows()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await MigratedDatabaseAsync();

        var tenantId = Guid.NewGuid();
        var client = Client.Create(
            tenantId, ClientType.Confidential, "Machine", GrantType.ClientCredentials,
            ClientAuthenticationMethod.PrivateKeyJwt, isFirstParty: false, consentRequired: false).Value;
        Assert.True(client.AddSecret(ClientSecret.Create(SecretHash, "initial", expiresAt: null)).IsSuccess);
        Assert.True(client.AddSigningKey(ClientSigningKey.Create("kid-1", "{}", "ES256", expiresAt: null)).IsSuccess);
        using var scope = BeginAdminScope(tenantId);

        await using (var db = Clients(connectionString))
        {
            db.Clients.Add(client);
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using (var db = Clients(connectionString))
        {
            var loaded = await db.Clients.Include(c => c.Secrets).Include(c => c.SigningKeys).SingleAsync();
            loaded.Delete();
            await db.SaveChangesAsync(AdminId, CancellationToken.None);
        }

        await using (var db = Clients(connectionString))
        {
            Assert.True((await db.Clients.SingleAsync()).IsDeleted);
            Assert.Equal(0, await db.Set<ClientSecret>().CountAsync());
            Assert.Equal(0, await db.Set<ClientSigningKey>().CountAsync());
        }
    }
}
