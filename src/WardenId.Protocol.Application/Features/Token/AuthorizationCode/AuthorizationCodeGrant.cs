namespace WardenId.Protocol.Application.Features.Token.AuthorizationCode;

/// <summary>What goes into the tokens for a redeemed code. User claims are null unless their scope was granted.</summary>
public sealed record AuthorizationCodeGrant(
    string Subject,
    Guid TenantId,
    string Audience,
    TimeSpan AccessTokenLifetime,
    TimeSpan IdTokenLifetime,
    string? PreferredUsername,
    string? Email,
    bool? EmailVerified);
