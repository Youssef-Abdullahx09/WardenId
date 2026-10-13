namespace WardenId.Protocol.Application.Features.Token;

/// <summary>
/// What goes into the tokens for a redeemed code or refresh token. User claims are null unless their
/// scope was granted; RefreshTokenLifetime is null unless offline_access was granted and the client still
/// holds the refresh grant.
/// </summary>
public sealed record UserTokenGrant(
    string Subject,
    Guid TenantId,
    string Audience,
    TimeSpan AccessTokenLifetime,
    TimeSpan IdTokenLifetime,
    TimeSpan? RefreshTokenLifetime,
    string? PreferredUsername,
    string? Email,
    bool? EmailVerified);
