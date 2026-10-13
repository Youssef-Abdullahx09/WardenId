namespace WardenId.Protocol.Infrastructure.OpenIddict;

/// <summary>Bound from the ProtocolOpenIddict configuration section.</summary>
public sealed class ProtocolOpenIddictPruningOptions
{
    public bool PruningEnabled { get; init; } = true;
    public TimeSpan PruneInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Rows younger than this are kept, even when already dead. Must be at least
    /// ProtocolToken:RefreshTokenAbsoluteLifetime (validated on start): a redeemed refresh token has to
    /// outlive the token's own lifetime, or replaying it later finds no row and the family is not revoked.
    /// </summary>
    public TimeSpan PruneMinimumAge { get; init; } = TimeSpan.FromDays(90);
}
