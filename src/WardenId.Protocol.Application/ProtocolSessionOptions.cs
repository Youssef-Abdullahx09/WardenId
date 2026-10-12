namespace WardenId.Protocol.Application;

/// <summary>Per-tenant login session (cookie) on the issuer path.</summary>
public sealed class ProtocolSessionOptions
{
    public TimeSpan CookieLifetime { get; init; } = TimeSpan.FromHours(8);
}
