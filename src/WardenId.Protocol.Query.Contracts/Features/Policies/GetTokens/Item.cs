namespace WardenId.Protocol.Query.Contracts.Features.Policies.GetTokens;

/// <summary>
/// The row of Features/Policies/GetTokens/Query.sql: the tenant's lifetime overrides. Columns are Postgres
/// <c>interval</c>, which Npgsql reads as <see cref="TimeSpan"/>; null = the global default applies.
/// </summary>
public sealed class Item
{
    public TimeSpan? AccessTokenLifetime { get; init; }
    public TimeSpan? IdTokenLifetime { get; init; }
    public TimeSpan? RefreshTokenLifetime { get; init; }
    public TimeSpan? RefreshTokenAbsoluteLifetime { get; init; }
    public TimeSpan? AuthorizationCodeLifetime { get; init; }
    public TimeSpan? SessionCookieLifetime { get; init; }
    public TimeSpan? SessionAbsoluteLifetime { get; init; }
}
