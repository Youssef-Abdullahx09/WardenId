namespace WardenId.Protocol.Application.Features.Policies.GetTokens;

/// <summary>
/// The lifetimes in effect for a tenant. Every lifetime is an ISO 8601 duration, e.g. "PT15M", "P30D", "P1DT12H".
/// </summary>
/// <param name="Scope">"tenant" when the tenant overrides at least one lifetime, else "global" (process configuration).</param>
/// <param name="Overrides">The tenant's own values; null fields fall back to the global default.</param>
public sealed record TokenPolicy(
    string AccessTokenLifetime,
    string IdTokenLifetime,
    string RefreshTokenLifetime,
    string RefreshTokenAbsoluteLifetime,
    string AuthorizationCodeLifetime,
    string SessionCookieLifetime,
    string SessionAbsoluteLifetime,
    string Scope,
    TokenPolicyOverrides Overrides);

/// <summary>A tenant's lifetime overrides as ISO 8601 durations; null = the global default applies.</summary>
public sealed record TokenPolicyOverrides(
    string? AccessTokenLifetime,
    string? IdTokenLifetime,
    string? RefreshTokenLifetime,
    string? RefreshTokenAbsoluteLifetime,
    string? AuthorizationCodeLifetime,
    string? SessionCookieLifetime,
    string? SessionAbsoluteLifetime);
