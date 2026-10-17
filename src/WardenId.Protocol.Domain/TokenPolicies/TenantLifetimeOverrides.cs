using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain.TokenPolicies;

/// <summary>
/// A tenant's token and login-session lifetimes. Every field is optional: null means "use the global default"
/// (ProtocolToken / ProtocolSession configuration). Bounds are checked here; the pairs that span the tenant and
/// the global defaults (e.g. a tenant refresh lifetime against the global absolute cap) are checked by the
/// application layer, which knows the defaults.
/// </summary>
public sealed class TenantLifetimeOverrides : ValueObject
{
    // Field names as the API spells them, so errors name what the caller sent.
    public const string AccessTokenField = "accessTokenLifetime";
    public const string IdTokenField = "idTokenLifetime";
    public const string RefreshTokenField = "refreshTokenLifetime";
    public const string RefreshTokenAbsoluteField = "refreshTokenAbsoluteLifetime";
    public const string AuthorizationCodeField = "authorizationCodeLifetime";
    public const string SessionCookieField = "sessionCookieLifetime";
    public const string SessionAbsoluteField = "sessionAbsoluteLifetime";

    // Access and ID tokens are bearer JWTs that can't be recalled before they expire: at least a minute so clock
    // skew doesn't kill them on arrival, at most a day so a leaked one stops working within a day.
    public static readonly TimeSpan MinAccessToken = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxAccessToken = TimeSpan.FromHours(24);
    public static readonly TimeSpan MinIdToken = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxIdToken = TimeSpan.FromHours(24);

    // Refresh tokens rotate and can be revoked; under an hour they'd outlive few access tokens, over a year
    // nobody re-authenticates.
    public static readonly TimeSpan MinRefreshToken = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxRefreshToken = TimeSpan.FromDays(365);
    public static readonly TimeSpan MinRefreshTokenAbsolute = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxRefreshTokenAbsolute = TimeSpan.FromDays(365);

    // RFC 6749 §4.1.2: codes are short-lived, 10 minutes at most; 30 seconds leaves time for a slow redirect.
    public static readonly TimeSpan MinAuthorizationCode = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxAuthorizationCode = TimeSpan.FromMinutes(10);

    // The idle (sliding) cookie: under 5 minutes users get signed out mid-form; over 30 days an unattended
    // browser stays signed in for a month.
    public static readonly TimeSpan MinSessionCookie = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxSessionCookie = TimeSpan.FromDays(30);
    public static readonly TimeSpan MinSessionAbsolute = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxSessionAbsolute = TimeSpan.FromDays(365);

    public TimeSpan? AccessToken { get; }
    public TimeSpan? IdToken { get; }
    public TimeSpan? RefreshToken { get; }
    public TimeSpan? RefreshTokenAbsolute { get; }
    public TimeSpan? AuthorizationCode { get; }
    public TimeSpan? SessionCookie { get; }
    public TimeSpan? SessionAbsolute { get; }

    public bool IsEmpty =>
        AccessToken is null && IdToken is null && RefreshToken is null && RefreshTokenAbsolute is null
        && AuthorizationCode is null && SessionCookie is null && SessionAbsolute is null;

    private TenantLifetimeOverrides(
        TimeSpan? accessToken, TimeSpan? idToken, TimeSpan? refreshToken, TimeSpan? refreshTokenAbsolute,
        TimeSpan? authorizationCode, TimeSpan? sessionCookie, TimeSpan? sessionAbsolute)
    {
        AccessToken = accessToken;
        IdToken = idToken;
        RefreshToken = refreshToken;
        RefreshTokenAbsolute = refreshTokenAbsolute;
        AuthorizationCode = authorizationCode;
        SessionCookie = sessionCookie;
        SessionAbsolute = sessionAbsolute;
    }

    public static TenantLifetimeOverrides None { get; } = new(null, null, null, null, null, null, null);

    public static Result<TenantLifetimeOverrides> Create(
        TimeSpan? accessToken, TimeSpan? idToken, TimeSpan? refreshToken, TimeSpan? refreshTokenAbsolute,
        TimeSpan? authorizationCode, TimeSpan? sessionCookie, TimeSpan? sessionAbsolute)
    {
        var errors = new[]
        {
            Within(AccessTokenField, accessToken, MinAccessToken, MaxAccessToken,
                "access tokens can't be recalled once issued, so they stay short"),
            Within(IdTokenField, idToken, MinIdToken, MaxIdToken,
                "ID tokens are read once at sign-in and are not meant to live long"),
            Within(RefreshTokenField, refreshToken, MinRefreshToken, MaxRefreshToken,
                "the sliding lifetime of each rotated refresh token"),
            Within(RefreshTokenAbsoluteField, refreshTokenAbsolute, MinRefreshTokenAbsolute, MaxRefreshTokenAbsolute,
                "the hard cap from sign-in after which the user must authenticate again"),
            Within(AuthorizationCodeField, authorizationCode, MinAuthorizationCode, MaxAuthorizationCode,
                "authorization codes must be short-lived (RFC 6749 §4.1.2)"),
            Within(SessionCookieField, sessionCookie, MinSessionCookie, MaxSessionCookie,
                "the idle timeout of the sign-in cookie"),
            Within(SessionAbsoluteField, sessionAbsolute, MinSessionAbsolute, MaxSessionAbsolute,
                "the hard cap of a login session from sign-in")
        }.Where(e => e is not null).ToList();
        if (errors.Count > 0) return Result.Fail(errors[0]!);

        if (refreshToken is { } sliding && refreshTokenAbsolute is { } absolute && absolute < sliding)
            return Result.Fail(TokenPolicyErrors.ShorterThan(RefreshTokenAbsoluteField, absolute, RefreshTokenField, sliding,
                "no refresh token outlives the absolute cap, so a shorter cap makes the sliding lifetime meaningless"));

        if (sessionCookie is { } cookie && sessionAbsolute is { } sessionCap && sessionCap < cookie)
            return Result.Fail(TokenPolicyErrors.ShorterThan(SessionAbsoluteField, sessionCap, SessionCookieField, cookie,
                "sliding the cookie never extends the session past its absolute cap"));

        return Result.Ok(new TenantLifetimeOverrides(
            accessToken, idToken, refreshToken, refreshTokenAbsolute, authorizationCode, sessionCookie, sessionAbsolute));
    }

    /// <summary>For values already validated (EF materialization, the aggregate's own fields).</summary>
    public static TenantLifetimeOverrides FromTrustedSource(
        TimeSpan? accessToken, TimeSpan? idToken, TimeSpan? refreshToken, TimeSpan? refreshTokenAbsolute,
        TimeSpan? authorizationCode, TimeSpan? sessionCookie, TimeSpan? sessionAbsolute)
        => new(accessToken, idToken, refreshToken, refreshTokenAbsolute, authorizationCode, sessionCookie, sessionAbsolute);

    private static FluentResults.Error? Within(string field, TimeSpan? value, TimeSpan min, TimeSpan max, string why)
    {
        if (value is not { } actual || (actual >= min && actual <= max)) return null;
        return TokenPolicyErrors.OutOfBounds(
            field, $"between {System.Xml.XmlConvert.ToString(min)} and {System.Xml.XmlConvert.ToString(max)}", why);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AccessToken;
        yield return IdToken;
        yield return RefreshToken;
        yield return RefreshTokenAbsolute;
        yield return AuthorizationCode;
        yield return SessionCookie;
        yield return SessionAbsolute;
    }
}
