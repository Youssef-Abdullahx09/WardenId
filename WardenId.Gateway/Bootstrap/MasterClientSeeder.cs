using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Infrastructure;

namespace WardenId.Gateway.Bootstrap;

/// <summary>
/// Seeds a first-party client into the master tenant from the "MasterTenant:Client" config section
/// (in development: Gatehouse's BFF, a confidential client) so the seeded admin can sign in through
/// /authorize without first creating a client through the admin API. Confidential clients need a
/// configured secret, stored hashed; public clients must not have one. The client_id is pinned from config (unlike API-created clients,
/// which get a generated one) so front-ends can be configured with it ahead of time. Never touches
/// an already-existing client — config changes have no effect once it has been created.
/// </summary>
public static class MasterClientSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        var section = app.Configuration.GetSection(ConfigurationKeys.MasterTenant.Section);
        if (!section.Exists()) return;

        var root = section.Get<MasterTenantClientOptions>();
        var options = root?.Client;
        if (options is null) return;

        if (string.IsNullOrWhiteSpace(root!.Slug) ||
            string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.DisplayName))
        {
            throw new InvalidOperationException(
                $"Configuration section '{ConfigurationKeys.MasterTenant.Section}:Client' is present but " +
                $"'{nameof(MasterTenantClientOptions.Slug)}', '{nameof(MasterClientSeedOptions.ClientId)}' and " +
                $"'{nameof(MasterClientSeedOptions.DisplayName)}' are required.");
        }

        var clientType = options.ClientType ?? ClientType.Confidential;
        if (clientType == ClientType.Confidential && string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new InvalidOperationException(
                $"Master client '{options.ClientId}' is confidential but " +
                $"'{nameof(MasterClientSeedOptions.Secret)}' is not configured.");
        }

        if (clientType == ClientType.Public && !string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new InvalidOperationException(
                $"Master client '{options.ClientId}' is public and cannot have a " +
                $"'{nameof(MasterClientSeedOptions.Secret)}'.");
        }

        var clientIdResult = ClientId.Create(options.ClientId);
        if (clientIdResult.IsFailed) throw InvalidConfig("client id", options.ClientId, clientIdResult.Errors);

        var redirectUris = ParseAll(options.RedirectUris, RedirectUri.Create, "redirect URI");
        var postLogoutRedirectUris = ParseAll(options.PostLogoutRedirectUris, RedirectUri.Create, "post-logout redirect URI");
        var corsOrigins = ParseAll(options.CorsOrigins, CorsOrigin.Create, "CORS origin");

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("WardenId.Gateway.MasterClientSeed");

        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var slug = Slug.FromTrustedSource(root.Slug);
        var tenantResult = await tenantRepository.GetBySlugAsync(slug, CancellationToken.None);
        if (tenantResult.IsFailed)
        {
            logger.LogError(
                "Master tenant '{Slug}' not found — skipping client seed. {Errors}",
                slug.Value, string.Join("; ", tenantResult.Errors.Select(e => e.Message)));
            return;
        }

        var tenantId = tenantResult.Value.Id;
        var clientId = clientIdResult.Value;

        var dbContext = scope.ServiceProvider.GetRequiredService<IClientManagementDbContext>();
        if (await dbContext.Clients.AnyAsync(c => c.TenantId == tenantId && c.ClientId == clientId, CancellationToken.None))
        {
            logger.LogInformation("Master client '{ClientId}' already exists — skipping seed.", clientId.Value);
            return;
        }

        var clientResult = Client.Create(
            tenantId,
            clientType,
            options.DisplayName,
            GrantType.AuthorizationCode | GrantType.RefreshToken,
            ClientAuthenticationMethod.SharedSecret,
            isFirstParty: true,
            consentRequired: false,
            description: "Seeded first-party client for the master tenant.",
            clientId: clientId);

        if (clientResult.IsFailed)
        {
            throw new InvalidOperationException(
                "Failed to seed master client: " + string.Join("; ", clientResult.Errors.Select(e => e.Message)));
        }

        var client = clientResult.Value;
        foreach (var uri in redirectUris) client.AddRedirectUri(uri);
        client.ReplacePostLogoutRedirectUris(postLogoutRedirectUris);
        foreach (var origin in corsOrigins) client.AddCorsOrigin(origin);

        if (clientType == ClientType.Confidential)
        {
            var secretResult = client.AddSecret(
                ClientSecret.Create(ClientSecretHasher.Hash(options.Secret!), "seeded", expiresAt: null));
            if (secretResult.IsFailed)
            {
                throw new InvalidOperationException(
                    "Failed to seed master client secret: " +
                    string.Join("; ", secretResult.Errors.Select(e => e.Message)));
            }
        }

        var repository = scope.ServiceProvider.GetRequiredService<IClientRepository>();

        // No request, so no AuditContextMiddleware: AuditCaptureInterceptor fails closed without an
        // ambient audit context, and a Client is audited. Same System actor pattern as MasterAdminSeeder.
        var auditContextAccessor = scope.ServiceProvider.GetRequiredService<IAuditContextAccessor>();
        var systemActor = Actor.Create(ActorType.System, "master-client-seeder", ActorScope.Platform()).Value;
        using (auditContextAccessor.BeginScope(
                   new AuditAmbientContext(systemActor, tenantId, Correlation.None(), ClientContext.Unknown())))
        {
            await repository.CreateAsync(client, Guid.Empty, CancellationToken.None);
        }

        logger.LogInformation(
            "Master client '{ClientId}' seeded for tenant '{Slug}'.", clientId.Value, slug.Value);
    }

    private static List<T> ParseAll<T>(
        IEnumerable<string>? raws, Func<string?, Result<T>> create, string what)
    {
        var parsed = new List<T>();
        foreach (var raw in raws ?? [])
        {
            var result = create(raw);
            if (result.IsFailed) throw InvalidConfig(what, raw, result.Errors);
            parsed.Add(result.Value);
        }

        return parsed;
    }

    private static InvalidOperationException InvalidConfig(
        string what, string? raw, IEnumerable<IError> errors) =>
        new($"Configured master client {what} '{raw}' is invalid: " +
            string.Join("; ", errors.Select(e => e.Message)));
}

internal sealed class MasterTenantClientOptions
{
    public string? Slug { get; init; }
    public MasterClientSeedOptions? Client { get; init; }
}

internal sealed class MasterClientSeedOptions
{
    public string? ClientId { get; init; }
    public string? DisplayName { get; init; }
    public ClientType? ClientType { get; init; }
    public string? Secret { get; init; }
    public string[]? RedirectUris { get; init; }
    public string[]? PostLogoutRedirectUris { get; init; }
    public string[]? CorsOrigins { get; init; }
}
