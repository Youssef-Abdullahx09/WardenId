namespace WardenId.Protocol.Infrastructure.Authentication;

public static class AccessTokenAuthenticationDefaults
{
    /// <summary>
    /// Authenticates REST callers by the access tokens WardenId itself issues
    /// (<see cref="AccessTokenAuthenticationHandler"/>).
    /// </summary>
    public const string AuthenticationScheme = "WardenId.AccessToken";

    public const string BearerPrefix = "Bearer ";
}
