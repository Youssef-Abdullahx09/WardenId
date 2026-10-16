using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Policies.GetTokens;
using GetTokensQuery = WardenId.Protocol.Application.Features.Policies.GetTokens.Query;

namespace WardenId.Protocol.Application.Tests.Features.Policies.GetTokens;

public class HandlerTests
{
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
        var handler = new Handler(Options.Create(tokens), Options.Create(sessions));

        var result = await handler.Handle(new GetTokensQuery(Guid.NewGuid()), CancellationToken.None);

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
                Scope: "global"),
            result.Value);
    }

    [Fact]
    public async Task Handle_ReturnsCodeDefaults_WhenNothingIsConfigured()
    {
        var handler = new Handler(Options.Create(new ProtocolTokenOptions()), Options.Create(new ProtocolSessionOptions()));

        var result = await handler.Handle(new GetTokensQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PT15M", result.Value.AccessTokenLifetime);
        Assert.Equal("PT15M", result.Value.IdTokenLifetime);
        Assert.Equal("P30D", result.Value.RefreshTokenLifetime);
        Assert.Equal("P90D", result.Value.RefreshTokenAbsoluteLifetime);
        Assert.Equal("PT5M", result.Value.AuthorizationCodeLifetime);
        Assert.Equal("PT8H", result.Value.SessionCookieLifetime);
        Assert.Equal("P1D", result.Value.SessionAbsoluteLifetime);
    }
}
