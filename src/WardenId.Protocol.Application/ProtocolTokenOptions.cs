namespace WardenId.Protocol.Application;

public sealed class ProtocolTokenOptions
{
    public string IssuerBaseUri { get; init; } = "https://wardenid.local";

    /// <summary>Access-token audience for client_credentials. "{issuer}" is replaced with the tenant issuer.</summary>
    public string DefaultAudience { get; init; } = "{issuer}/api";

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan IdTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
}
