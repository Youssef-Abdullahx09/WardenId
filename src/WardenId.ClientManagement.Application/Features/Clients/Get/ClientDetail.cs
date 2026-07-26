using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Get;

public sealed record ClientSecretSummary(Guid Id, string Label, DateTimeOffset? ExpiresAt);

public sealed record ClientSigningKeySummary(Guid Id, string Kid, string Algorithm, DateTimeOffset? ExpiresAt);

public sealed record ClientDetail(
    Guid Id,
    string ClientId,
    Guid TenantId,
    ClientType ClientType,
    string DisplayName,
    string? Description,
    bool Enabled,
    bool IsFirstParty,
    GrantType GrantType,
    bool RequirePkce,
    ClientAuthenticationMethod ClientAuthenticationMethod,
    bool ConsentRequired,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    string[] RedirectUris,
    string[] PostLogoutRedirectUris,
    string[] CorsOrigins,
    ClientSecretSummary[] Secrets,
    ClientSigningKeySummary[] SigningKeys,
    Guid[] AssignedScopeIds);
