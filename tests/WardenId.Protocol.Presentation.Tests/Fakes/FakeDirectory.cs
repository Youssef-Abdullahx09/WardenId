using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;

namespace WardenId.Protocol.Presentation.Tests.Fakes;

/// <summary>In-memory stand-in for Tenancy (tenants + ES256 keys) and ClientManagement (clients + secrets).</summary>
public sealed class FakeDirectory
{
    public const string AcmeSecret = "acme-primary";
    public const string AcmeRotatedSecret = "acme-rotated";
    public const string GlobexSecret = "globex-primary";

    public sealed record TenantEntry(Guid TenantId, string Slug, TenantIssuanceStatus Status, Guid KeyId, ECDsa Key);

    public sealed record ClientEntry(
        Guid TenantId, Guid Id, string ClientId, ClientKind Kind, bool Enabled,
        IReadOnlyList<AllowedGrant> Grants, ClientAuthMethod Auth, int? AccessTokenLifetimeSeconds,
        int? RefreshTokenLifetimeSeconds, string[] Secrets, string[] RedirectUris, bool RequirePkce,
        string[] PostLogoutRedirectUris, string? BackchannelLogoutUri, int? IdTokenLifetimeSeconds = null,
        string[]? CorsOriginList = null)
    {
        public string[] CorsOrigins => CorsOriginList ?? [];
    }

    public const string SpaOrigin = "https://spa.example";

    /// <summary>How often Protocol asked ClientManagement for a tenant's CORS origins (cache tests).</summary>
    public int CorsOriginLookups { get; set; }

    public const string WebRedirect = "https://app.example/cb";
    public const string SpaRedirect = "https://spa.example/cb";

    public const string SpaRtRedirect = "https://spa-rt.example/cb";

    public const string WebLoggedOut = "https://app.example/logged-out";

    public const string UserPassword = "correct horse";

    public sealed class UserEntry(Guid tenantId, string username, UserStatus status, params RequiredAction[] pendingActions)
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = Guid.NewGuid();
        public string Username { get; } = username;
        public string Email => $"{Username}@example.test";
        public UserStatus Status { get; set; } = status;
        public List<RequiredAction> PendingActions { get; } = [.. pendingActions];
    }

    public List<UserEntry> Users { get; } = [];

    public UserEntry User(string username) => Users.Single(u => u.Username == username);

    public Dictionary<string, TenantEntry> Tenants { get; } = new(StringComparer.Ordinal);
    public List<ClientEntry> Clients { get; } = [];

    public FakeDirectory()
    {
        var acme = AddTenant("acme", TenantIssuanceStatus.Active);
        var globex = AddTenant("globex", TenantIssuanceStatus.Active);
        var frozen = AddTenant("frozen", TenantIssuanceStatus.Suspended);

        AddClient(acme, "svc", secrets: [AcmeSecret, AcmeRotatedSecret]);
        AddClient(globex, "svc", secrets: [GlobexSecret]);
        AddClient(frozen, "svc", secrets: [AcmeSecret]);
        AddClient(acme, "short", lifetime: 60, secrets: [AcmeSecret]);
        AddClient(acme, "no-cc", grants: [AllowedGrant.AuthorizationCode], secrets: [AcmeSecret]);
        AddClient(acme, "disabled", enabled: false, secrets: [AcmeSecret]);
        AddClient(acme, "pkjwt", auth: ClientAuthMethod.PrivateKeyJwt, secrets: [AcmeSecret]);
        AddClient(acme, "public-app", kind: ClientKind.Public, secrets: []);
        AddClient(acme, "web", grants: [AllowedGrant.AuthorizationCode], secrets: [AcmeSecret], redirectUris: [WebRedirect]);
        AddClient(acme, "spa", kind: ClientKind.Public, grants: [AllowedGrant.AuthorizationCode], redirectUris: [SpaRedirect],
            corsOrigins: [SpaOrigin]);
        AddClient(globex, "web", grants: [AllowedGrant.AuthorizationCode], secrets: [GlobexSecret], redirectUris: [WebRedirect]);
        AddClient(acme, "web-rt", grants: [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken],
            secrets: [AcmeSecret], redirectUris: [WebRedirect],
            postLogoutRedirectUris: [WebLoggedOut], backchannelLogoutUri: "https://app.example/bcl");
        AddClient(acme, "web-rt-other", grants: [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken],
            secrets: [AcmeSecret], redirectUris: [WebRedirect]);
        AddClient(acme, "spa-rt", kind: ClientKind.Public, grants: [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken],
            redirectUris: [SpaRtRedirect]);
        AddClient(acme, "web-short-id", grants: [AllowedGrant.AuthorizationCode], secrets: [AcmeSecret],
            redirectUris: [WebRedirect], idTokenLifetime: 120);
        AddClient(globex, "web-rt", grants: [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken],
            secrets: [GlobexSecret], redirectUris: [WebRedirect]);

        Users.Add(new UserEntry(acme.TenantId, "alice", UserStatus.Active));
        Users.Add(new UserEntry(acme.TenantId, "dave", UserStatus.Active));
        Users.Add(new UserEntry(acme.TenantId, "bob", UserStatus.PendingVerification, RequiredAction.VerifyEmail));
        Users.Add(new UserEntry(acme.TenantId, "carol", UserStatus.Disabled));
    }

    public TenantEntry Tenant(string slug) => Tenants[slug];

    /// <summary>The tenant's public key as the stored JWK JSON Tenancy serves (EC P-256, public members only).</summary>
    public static string PublicJwk(TenantEntry tenant)
    {
        var q = tenant.Key.ExportParameters(includePrivateParameters: false).Q;
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["use"] = "sig",
            ["alg"] = "ES256",
            ["kid"] = tenant.KeyId.ToString("N"),
            ["x"] = Base64UrlEncoder.Encode(q.X!),
            ["y"] = Base64UrlEncoder.Encode(q.Y!)
        });
    }

    public ClientEntry Client(string slug, string clientId) => FindClient(Tenant(slug).TenantId, clientId)!;

    public ClientEntry? FindClient(Guid tenantId, string clientId) =>
        Clients.FirstOrDefault(c => c.TenantId == tenantId && c.ClientId == clientId);

    private TenantEntry AddTenant(string slug, TenantIssuanceStatus status)
    {
        var entry = new TenantEntry(Guid.NewGuid(), slug, status, Guid.NewGuid(), ECDsa.Create(ECCurve.NamedCurves.nistP256));
        Tenants[slug] = entry;
        return entry;
    }

    private void AddClient(
        TenantEntry tenant, string clientId,
        ClientKind kind = ClientKind.Confidential, bool enabled = true,
        AllowedGrant[]? grants = null, ClientAuthMethod auth = ClientAuthMethod.SharedSecret,
        int? lifetime = null, string[]? secrets = null, string[]? redirectUris = null, bool? requirePkce = null,
        int? refreshLifetime = null, string[]? postLogoutRedirectUris = null, string? backchannelLogoutUri = null,
        int? idTokenLifetime = null, string[]? corsOrigins = null) =>
        Clients.Add(new ClientEntry(
            tenant.TenantId, Guid.NewGuid(), clientId, kind, enabled,
            grants ?? [AllowedGrant.ClientCredentials], auth, lifetime, refreshLifetime, secrets ?? [],
            redirectUris ?? [], requirePkce ?? kind == ClientKind.Public,
            postLogoutRedirectUris ?? [], backchannelLogoutUri, idTokenLifetime, corsOrigins));
}
