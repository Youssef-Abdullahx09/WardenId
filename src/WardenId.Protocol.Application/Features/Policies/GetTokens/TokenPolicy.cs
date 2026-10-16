namespace WardenId.Protocol.Application.Features.Policies.GetTokens;

/// <summary>Every lifetime is an ISO 8601 duration, e.g. "PT15M", "P30D", "P1DT12H".</summary>
/// <param name="Scope">Where the values come from: "global" (process configuration) until per-tenant policies exist.</param>
public sealed record TokenPolicy(
    string AccessTokenLifetime,
    string IdTokenLifetime,
    string RefreshTokenLifetime,
    string RefreshTokenAbsoluteLifetime,
    string AuthorizationCodeLifetime,
    string SessionCookieLifetime,
    string SessionAbsoluteLifetime,
    string Scope);
