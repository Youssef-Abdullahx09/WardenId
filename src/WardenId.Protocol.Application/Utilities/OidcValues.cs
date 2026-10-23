namespace WardenId.Protocol.Application.Utilities;

/// <summary>
/// OAuth 2.0 / OIDC wire values this layer needs without OpenIddict (which only Infrastructure and Presentation
/// reference); the values match OpenIddictConstants where it defines them.
/// </summary>
public static class OidcValues
{
    public static class ResponseTypes
    {
        public const string Code = "code";
    }

    public static class SubjectTypes
    {
        public const string Public = "public";
    }

    public static class GrantTypes
    {
        public const string AuthorizationCode = "authorization_code";
        public const string ClientCredentials = "client_credentials";
        public const string RefreshToken = "refresh_token";
    }

    public static class ClientAuthenticationMethods
    {
        public const string ClientSecretBasic = "client_secret_basic";
        public const string ClientSecretPost = "client_secret_post";
        public const string None = "none";
    }

    public static class CodeChallengeMethods
    {
        public const string S256 = "S256";
    }

    /// <summary>Request parameters OpenIddict has no constant for.</summary>
    public static class Parameters
    {
        /// <summary>RP-Initiated Logout 1.0 §2.</summary>
        public const string LogoutHint = "logout_hint";

        /// <summary>Back-Channel Logout 1.0 §2.5.</summary>
        public const string LogoutToken = "logout_token";
    }

    /// <summary>Claims OpenIddict has no constant for.</summary>
    public static class Claims
    {
        /// <summary>Back-Channel Logout 1.0 §2.4: the logout token's events claim.</summary>
        public const string Events = "events";
    }
}
