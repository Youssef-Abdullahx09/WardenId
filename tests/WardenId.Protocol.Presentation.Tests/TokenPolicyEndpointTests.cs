using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;
using GetTokensEndpoint = WardenId.Protocol.Presentation.Features.Policies.GetTokens.Endpoint;

namespace WardenId.Protocol.Presentation.Tests;

public class TokenPolicyEndpointTests
{
    private const string Path = "/api/v1/admin/tenants/0198c6b0-0000-7000-8000-000000000001/policies/tokens";

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProtocolToken:AccessTokenLifetime"] = "00:10:00",
            ["ProtocolSession:AbsoluteLifetime"] = "1.12:00:00"
        });
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddProtocolApplication(builder.Configuration);
        builder.Services.AddAuthentication(TestAuthenticationHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.Scheme, null);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        new GetTokensEndpoint().MapEndpoint(app);
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Get_ReturnsLifetimesAsIso8601Durations_ForAnAuthenticatedCaller()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.Header, "admin");

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal("PT10M", root.GetProperty("accessTokenLifetime").GetString());
        Assert.Equal("PT15M", root.GetProperty("idTokenLifetime").GetString());
        Assert.Equal("P30D", root.GetProperty("refreshTokenLifetime").GetString());
        Assert.Equal("P90D", root.GetProperty("refreshTokenAbsoluteLifetime").GetString());
        Assert.Equal("PT5M", root.GetProperty("authorizationCodeLifetime").GetString());
        Assert.Equal("PT8H", root.GetProperty("sessionCookieLifetime").GetString());
        Assert.Equal("P1DT12H", root.GetProperty("sessionAbsoluteLifetime").GetString());
        Assert.Equal("global", root.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Get_Returns401_WithoutAnAuthenticatedCaller()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "Test";
        public const string Header = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var user))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim("sub", user.ToString())], Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
