using WardenId.Protocol.Domain.TokenPolicies;
using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain.Tests;

public class TenantTokenPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static string ErrorCode(FluentResults.ResultBase result) =>
        (string)result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey];

    private static TenantLifetimeOverrides Overrides(
        TimeSpan? accessToken = null, TimeSpan? idToken = null, TimeSpan? refreshToken = null,
        TimeSpan? refreshTokenAbsolute = null, TimeSpan? authorizationCode = null,
        TimeSpan? sessionCookie = null, TimeSpan? sessionAbsolute = null) =>
        TenantLifetimeOverrides.Create(
            accessToken, idToken, refreshToken, refreshTokenAbsolute, authorizationCode, sessionCookie, sessionAbsolute).Value;

    [Fact]
    public void Overrides_AllNull_IsValid_AndEmpty()
    {
        var result = TenantLifetimeOverrides.Create(null, null, null, null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEmpty);
    }

    [Fact]
    public void Overrides_InsideBounds_AreKept()
    {
        var overrides = Overrides(
            accessToken: TimeSpan.FromMinutes(5),
            idToken: TimeSpan.FromMinutes(10),
            refreshToken: TimeSpan.FromDays(7),
            refreshTokenAbsolute: TimeSpan.FromDays(30),
            authorizationCode: TimeSpan.FromSeconds(60),
            sessionCookie: TimeSpan.FromHours(1),
            sessionAbsolute: TimeSpan.FromHours(12));

        Assert.False(overrides.IsEmpty);
        Assert.Equal(TimeSpan.FromMinutes(5), overrides.AccessToken);
        Assert.Equal(TimeSpan.FromMinutes(10), overrides.IdToken);
        Assert.Equal(TimeSpan.FromDays(7), overrides.RefreshToken);
        Assert.Equal(TimeSpan.FromDays(30), overrides.RefreshTokenAbsolute);
        Assert.Equal(TimeSpan.FromSeconds(60), overrides.AuthorizationCode);
        Assert.Equal(TimeSpan.FromHours(1), overrides.SessionCookie);
        Assert.Equal(TimeSpan.FromHours(12), overrides.SessionAbsolute);
    }

    public static TheoryData<string, TimeSpan> OutOfBounds => new()
    {
        { "accessTokenLifetime", TimeSpan.FromSeconds(59) },
        { "accessTokenLifetime", TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1) },
        { "idTokenLifetime", TimeSpan.FromSeconds(59) },
        { "idTokenLifetime", TimeSpan.FromHours(25) },
        { "refreshTokenLifetime", TimeSpan.FromMinutes(59) },
        { "refreshTokenLifetime", TimeSpan.FromDays(366) },
        { "refreshTokenAbsoluteLifetime", TimeSpan.FromMinutes(59) },
        { "refreshTokenAbsoluteLifetime", TimeSpan.FromDays(366) },
        { "authorizationCodeLifetime", TimeSpan.FromSeconds(29) },
        { "authorizationCodeLifetime", TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1) },
        { "sessionCookieLifetime", TimeSpan.FromMinutes(4) },
        { "sessionCookieLifetime", TimeSpan.FromDays(31) },
        { "sessionAbsoluteLifetime", TimeSpan.FromMinutes(4) },
        { "sessionAbsoluteLifetime", TimeSpan.FromDays(366) }
    };

    [Theory]
    [MemberData(nameof(OutOfBounds))]
    public void Overrides_OutOfBounds_FailValidation_NamingTheField(string field, TimeSpan value)
    {
        var result = field switch
        {
            "accessTokenLifetime" => TenantLifetimeOverrides.Create(value, null, null, null, null, null, null),
            "idTokenLifetime" => TenantLifetimeOverrides.Create(null, value, null, null, null, null, null),
            "refreshTokenLifetime" => TenantLifetimeOverrides.Create(null, null, value, null, null, null, null),
            "refreshTokenAbsoluteLifetime" => TenantLifetimeOverrides.Create(null, null, null, value, null, null, null),
            "authorizationCodeLifetime" => TenantLifetimeOverrides.Create(null, null, null, null, value, null, null),
            "sessionCookieLifetime" => TenantLifetimeOverrides.Create(null, null, null, null, null, value, null),
            _ => TenantLifetimeOverrides.Create(null, null, null, null, null, null, value)
        };

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, ErrorCode(result));
        Assert.StartsWith(field, result.Errors[0].Message);
    }

    [Fact]
    public void Overrides_AtBounds_AreValid()
    {
        Assert.True(TenantLifetimeOverrides.Create(
            TimeSpan.FromMinutes(1), TimeSpan.FromHours(24), TimeSpan.FromHours(1), TimeSpan.FromHours(1),
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)).IsSuccess);
        Assert.True(TenantLifetimeOverrides.Create(
            TimeSpan.FromHours(24), TimeSpan.FromMinutes(1), TimeSpan.FromDays(365), TimeSpan.FromDays(365),
            TimeSpan.FromMinutes(10), TimeSpan.FromDays(30), TimeSpan.FromDays(365)).IsSuccess);
    }

    [Fact]
    public void Overrides_RefreshAbsoluteShorterThanRefresh_FailsValidation()
    {
        var result = TenantLifetimeOverrides.Create(
            null, null, TimeSpan.FromDays(30), TimeSpan.FromDays(7), null, null, null);

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, ErrorCode(result));
        Assert.StartsWith("refreshTokenAbsoluteLifetime", result.Errors[0].Message);
    }

    [Fact]
    public void Overrides_SessionAbsoluteShorterThanCookie_FailsValidation()
    {
        var result = TenantLifetimeOverrides.Create(
            null, null, null, null, null, TimeSpan.FromHours(8), TimeSpan.FromHours(1));

        Assert.True(result.IsFailed);
        Assert.StartsWith("sessionAbsoluteLifetime", result.Errors[0].Message);
    }

    [Fact]
    public void Create_StoresTheOverrides_ForTheTenant()
    {
        var policy = TenantTokenPolicy.Create(TenantId, Overrides(accessToken: TimeSpan.FromMinutes(5)));

        Assert.Equal(TenantId, policy.TenantId);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.AccessTokenLifetime);
        Assert.Null(policy.IdTokenLifetime);
        Assert.Equal(Overrides(accessToken: TimeSpan.FromMinutes(5)), policy.Overrides);
    }

    [Fact]
    public void Replace_ReplacesEveryField_NullClears()
    {
        var policy = TenantTokenPolicy.Create(
            TenantId, Overrides(accessToken: TimeSpan.FromMinutes(5), sessionCookie: TimeSpan.FromHours(1)));

        policy.Replace(Overrides(idToken: TimeSpan.FromMinutes(2)));

        Assert.Null(policy.AccessTokenLifetime);
        Assert.Null(policy.SessionCookieLifetime);
        Assert.Equal(TimeSpan.FromMinutes(2), policy.IdTokenLifetime);
    }

    [Fact]
    public void Policy_IsAuditedAsAdminConfig_AgainstTheTenant()
    {
        IAuditableAggregate policy = TenantTokenPolicy.Create(TenantId, Overrides());

        Assert.Equal("tenant", policy.AuditTargetTypeKey);
        Assert.Equal(AuditableChannel.AdminConfig, policy.AuditChannel);
        Assert.Equal(TenantId.ToString(), policy.AuditTargetId);
        Assert.Empty(policy.AuditSensitivePropertyNames);
    }
}
