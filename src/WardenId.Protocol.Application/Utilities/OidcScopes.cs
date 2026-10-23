namespace WardenId.Protocol.Application.Utilities;

/// <summary>
/// Hard-coded OIDC scopes until per-client scopes land (FR-CM-04). Discovery advertises exactly
/// these (offline_access is only honoured for clients allowed the RefreshToken grant); OpenIddict registers exactly these (pinned by the discovery drift test).
/// </summary>
public static class OidcScopes
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string Email = "email";
    public const string OfflineAccess = "offline_access";

    public static IReadOnlyList<string> Supported { get; } = [OpenId, Profile, Email, OfflineAccess];
}
