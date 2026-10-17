using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Policies.GetTokens;
using WardenId.Protocol.Domain.TokenPolicies;
using GetTokensQuery = WardenId.Protocol.Application.Features.Policies.GetTokens.Query;

namespace WardenId.Protocol.Application.Tests.Features.Policies.GetTokens;

public class HandlerTests
{
    private static readonly TokenPolicyOverrides NoOverrides = new(null, null, null, null, null, null, null);

    private static Handler CreateHandler(
        TestProtocolDbContext db, ProtocolTokenOptions? tokens = null, ProtocolSessionOptions? sessions = null) =>
        new(db, Options.Create(tokens ?? new ProtocolTokenOptions()), Options.Create(sessions ?? new ProtocolSessionOptions()));

    [Fact]
    public async Task Handle_ReturnsConfiguredLifetimes_AsIso8601Durations()
    {
        var tokens = new ProtocolTokenOptions
        {
            AccessTokenLifetime = TimeSpan.FromMinutes(10),
            IdTokenLifetime = TimeSpan.FromMinutes(5),
            RefreshTokenLifetime = TimeSpan.FromDays(14),
            RefreshTokenAbsoluteLifetime = TimeSpan.FromDays(60),
            AuthorizationCodeLifetime = TimeSpan.FromSeconds(90)
        };
        var sessions = new ProtocolSessionOptions
        {
            CookieLifetime = TimeSpan.FromHours(2),
            AbsoluteLifetime = TimeSpan.FromHours(36)
        };
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());

        var result = await CreateHandler(db, tokens, sessions).Handle(new GetTokensQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new TokenPolicy(
                AccessTokenLifetime: "PT10M",
                IdTokenLifetime: "PT5M",
                RefreshTokenLifetime: "P14D",
                RefreshTokenAbsoluteLifetime: "P60D",
                AuthorizationCodeLifetime: "PT1M30S",
                SessionCookieLifetime: "PT2H",
                SessionAbsoluteLifetime: "P1DT12H",
                Scope: "global",
                Overrides: NoOverrides),
            result.Value);
    }

    [Fact]
    public async Task Handle_ReturnsCodeDefaults_WhenNothingIsConfigured()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());

        var result = await CreateHandler(db).Handle(new GetTokensQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PT15M", result.Value.AccessTokenLifetime);
        Assert.Equal("PT15M", result.Value.IdTokenLifetime);
        Assert.Equal("P30D", result.Value.RefreshTokenLifetime);
        Assert.Equal("P90D", result.Value.RefreshTokenAbsoluteLifetime);
        Assert.Equal("PT5M", result.Value.AuthorizationCodeLifetime);
        Assert.Equal("PT8H", result.Value.SessionCookieLifetime);
        Assert.Equal("P1D", result.Value.SessionAbsoluteLifetime);
    }

    [Fact]
    public async Task Handle_WithTenantPolicy_ReturnsEffectiveValues_OverridesAndTenantScope()
    {
        var tenantId = Guid.NewGuid();
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        db.TenantTokenPolicies.Add(TenantTokenPolicy.Create(tenantId, TenantLifetimeOverrides.Create(
            TimeSpan.FromMinutes(5), null, null, null, TimeSpan.FromSeconds(45), TimeSpan.FromHours(1), null).Value));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(new GetTokensQuery(tenantId), CancellationToken.None);

        Assert.Equal("tenant", result.Value.Scope);
        Assert.Equal("PT5M", result.Value.AccessTokenLifetime);
        Assert.Equal("PT15M", result.Value.IdTokenLifetime);
        Assert.Equal("PT45S", result.Value.AuthorizationCodeLifetime);
        Assert.Equal("PT1H", result.Value.SessionCookieLifetime);
        Assert.Equal(new TokenPolicyOverrides("PT5M", null, null, null, "PT45S", "PT1H", null), result.Value.Overrides);
    }

    [Fact]
    public async Task Handle_WithAllOverridesCleared_IsGlobalScope()
    {
        var tenantId = Guid.NewGuid();
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        db.TenantTokenPolicies.Add(TenantTokenPolicy.Create(tenantId, TenantLifetimeOverrides.None));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(new GetTokensQuery(tenantId), CancellationToken.None);

        Assert.Equal("global", result.Value.Scope);
        Assert.Equal(NoOverrides, result.Value.Overrides);
    }
}
