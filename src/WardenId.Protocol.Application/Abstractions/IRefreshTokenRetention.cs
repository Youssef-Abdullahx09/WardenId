namespace WardenId.Protocol.Application.Abstractions;

/// <summary>
/// How long redeemed refresh tokens are kept before pruning (OpenIddict's PruneMinimumAge), or null when pruning is
/// off. A refresh-token absolute lifetime longer than this would let a replayed token find no row, so the family
/// would not be revoked; the global setting is checked on start, a tenant override when it is saved.
/// </summary>
public interface IRefreshTokenRetention
{
    TimeSpan? MinimumRetention { get; }
}
