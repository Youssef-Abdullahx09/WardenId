namespace WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;

/// <summary>
/// What the protocol layer needs to authenticate and authorize a client. Deliberately carries
/// no secret material — secrets are verified inside ClientManagement via VerifySecret.
/// </summary>
public sealed record ClientForAuthentication(
    Guid Id,
    string ClientId,
    ClientKind Kind,
    bool Enabled,
    IReadOnlyList<AllowedGrant> AllowedGrants,
    ClientAuthMethod AuthenticationMethod,
    int? AccessTokenLifetimeSeconds,
    int? RefreshTokenLifetimeSeconds,
    IReadOnlyList<string> RedirectUris,
    bool RequirePkce);
