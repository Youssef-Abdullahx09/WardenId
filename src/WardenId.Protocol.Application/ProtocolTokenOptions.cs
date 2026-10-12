namespace WardenId.Protocol.Application;

public sealed class ProtocolTokenOptions
{
    public string IssuerBaseUri { get; init; } = "https://wardenid.local";

    /// <summary>Access-token audience template. "{issuer}" is replaced with the tenant issuer.</summary>
    public string DefaultAudience { get; init; } = "{issuer}/api";

    /// <summary>Access-token audience for a tenant: DefaultAudience with "{issuer}" replaced.</summary>
    public string AudienceFor(string slug) =>
        DefaultAudience.Replace("{issuer}", IssuerUri.For(IssuerBaseUri, slug), StringComparison.Ordinal);

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan IdTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
    public TimeSpan AuthorizationCodeLifetime { get; init; } = TimeSpan.FromMinutes(5);
}
