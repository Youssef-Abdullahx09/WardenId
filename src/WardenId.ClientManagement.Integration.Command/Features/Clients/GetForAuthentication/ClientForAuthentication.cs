namespace WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;

/// <summary>
/// What the protocol layer needs to authenticate and authorize a client. Deliberately carries
/// no secret material — secrets are verified inside ClientManagement via VerifySecret.
/// <see cref="PostLogoutRedirectUris"/> feed OpenIddict's exact-match end-session validation;
/// <see cref="BackchannelLogoutUri"/> is null when the client does not take back-channel logout.
/// The lifetime overrides are null when the client uses the global <c>ProtocolToken</c> default.
/// <see cref="IdTokenLifetimeSeconds"/> was added last, with a default, so existing callers keep compiling.
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
    bool RequirePkce,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string? BackchannelLogoutUri,
    int? IdTokenLifetimeSeconds = null);
