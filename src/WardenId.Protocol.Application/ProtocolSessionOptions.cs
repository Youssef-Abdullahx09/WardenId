namespace WardenId.Protocol.Application;

/// <summary>Per-tenant login session (cookie + server-side Session row) on the issuer path.</summary>
public sealed class ProtocolSessionOptions
{
    /// <summary>Sliding idle lifetime of the tenant cookie.</summary>
    public TimeSpan CookieLifetime { get; init; } = TimeSpan.FromHours(8);

    /// <summary>Hard cap from sign-in (Session.ExpiresAt); sliding the cookie never extends it.</summary>
    public TimeSpan AbsoluteLifetime { get; init; } = TimeSpan.FromDays(1);

    /// <summary>Ended/expired session rows older than this are pruned. Must be at least AbsoluteLifetime (validated on start).</summary>
    public TimeSpan PruneMinimumAge { get; init; } = TimeSpan.FromDays(30);
}
