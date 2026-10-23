namespace WardenId.Protocol.Application.Utilities;

/// <summary>
/// Paths under a tenant issuer (<c>/tenants/{slug}/...</c>): the OIDC endpoints, relative to the issuer, and the
/// sign-in/account pages. Changing one breaks every relying party or bookmarked page.
/// </summary>
public static class ProtocolPaths
{
    /// <summary>The first path segment of every tenant issuer.</summary>
    public const string TenantsSegment = "tenants";

    public static class Endpoints
    {
        public const string Discovery = ".well-known/openid-configuration";
        public const string Jwks = "jwks";
        public const string Authorize = "authorize";
        public const string Token = "token";
        public const string Introspect = "introspect";
        public const string Revoke = "revoke";
        public const string UserInfo = "userinfo";
        public const string EndSession = "endsession";
    }

    public static class Pages
    {
        public const string Login = "login";
        public const string LoginCode = Login + "/code";
        public const string LoginTerms = Login + "/terms";
        public const string LoginEnrollTotp = Login + "/enroll-totp";
        public const string Logout = "logout";
        public const string SignedOut = "signed-out";
        public const string Account = "account";
        public const string AccountPassword = Account + "/password";
        public const string AccountAuthenticator = Account + "/authenticator";
        public const string AccountReplaceAuthenticator = AccountAuthenticator + "/replace";
        public const string AccountRecoveryCodes = Account + "/recovery-codes";
        public const string AccountRemoveMfa = Account + "/two-step/remove";
    }

    /// <summary><c>/tenants/{slug}</c>.</summary>
    public static string Tenant(string slug) => $"/{TenantsSegment}/{slug}";

    /// <summary><c>/tenants/{slug}/{path}</c>.</summary>
    public static string Tenant(string slug, string path) => $"/{TenantsSegment}/{slug}/{path}";
}
