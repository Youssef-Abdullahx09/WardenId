using WardenId.Protocol.Domain.TokenPolicies;

namespace WardenId.Protocol.Application.Features.Policies;

/// <summary>
/// The lifetimes in effect for one tenant: the tenant's override where it has one, else the global default
/// (ProtocolToken / ProtocolSession configuration). A client's own override wins over both — use the
/// <c>…For(clientSeconds)</c> helpers at issuance.
/// </summary>
public sealed record EffectiveLifetimes(
    TimeSpan AccessToken,
    TimeSpan IdToken,
    TimeSpan RefreshToken,
    TimeSpan RefreshTokenAbsolute,
    TimeSpan AuthorizationCode,
    TimeSpan SessionCookie,
    TimeSpan SessionAbsolute,
    TenantLifetimeOverrides Overrides)
{
    public static EffectiveLifetimes Resolve(
        ProtocolTokenOptions tokens, ProtocolSessionOptions sessions, TenantLifetimeOverrides? overrides)
    {
        var tenant = overrides ?? TenantLifetimeOverrides.None;
        return new EffectiveLifetimes(
            tenant.AccessToken ?? tokens.AccessTokenLifetime,
            tenant.IdToken ?? tokens.IdTokenLifetime,
            tenant.RefreshToken ?? tokens.RefreshTokenLifetime,
            tenant.RefreshTokenAbsolute ?? tokens.RefreshTokenAbsoluteLifetime,
            tenant.AuthorizationCode ?? tokens.AuthorizationCodeLifetime,
            tenant.SessionCookie ?? sessions.CookieLifetime,
            tenant.SessionAbsolute ?? sessions.AbsoluteLifetime,
            tenant);
    }

    /// <summary>True when the tenant overrides at least one lifetime.</summary>
    public bool IsTenantScoped => !Overrides.IsEmpty;

    public TimeSpan AccessTokenFor(int? clientSeconds) => clientSeconds is { } s ? TimeSpan.FromSeconds(s) : AccessToken;
    public TimeSpan IdTokenFor(int? clientSeconds) => clientSeconds is { } s ? TimeSpan.FromSeconds(s) : IdToken;
    public TimeSpan RefreshTokenFor(int? clientSeconds) => clientSeconds is { } s ? TimeSpan.FromSeconds(s) : RefreshToken;
}
