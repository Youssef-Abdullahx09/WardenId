namespace WardenId.Protocol.Application.Features.Grants;

/// <summary>An app the user signed in to: one OpenIddict authorization.</summary>
/// <param name="ClientId">The client's public client_id.</param>
/// <param name="OfflineAccess">Granted offline_access: its refresh tokens outlive the login session.</param>
public sealed record UserGrant(
    string AuthorizationId,
    string ClientId,
    DateTimeOffset? CreatedAt,
    IReadOnlyList<string> Scopes,
    bool OfflineAccess,
    GrantStatus Status);
