namespace WardenId.Protocol.Application;

public sealed class ProtocolTokenOptions
{
    public string IssuerBaseUri { get; init; } = "https://wardenid.local";
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan IdTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
}
