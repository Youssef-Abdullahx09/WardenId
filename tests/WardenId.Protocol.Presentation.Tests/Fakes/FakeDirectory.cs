using System.Security.Cryptography;
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
        IReadOnlyList<AllowedGrant> Grants, ClientAuthMethod Auth, int? AccessTokenLifetimeSeconds, string[] Secrets);

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
    }

    public TenantEntry Tenant(string slug) => Tenants[slug];

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
        int? lifetime = null, string[]? secrets = null) =>
        Clients.Add(new ClientEntry(
            tenant.TenantId, Guid.NewGuid(), clientId, kind, enabled,
            grants ?? [AllowedGrant.ClientCredentials], auth, lifetime, secrets ?? []));
}
