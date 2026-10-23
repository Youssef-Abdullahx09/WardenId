using System.Net;
using System.Net.Http.Json;
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
using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.Protocol.Query.Contracts;
using WardenId.SharedKernel.Presentation;
using GetTokensEndpoint = WardenId.Protocol.Presentation.Features.Policies.GetTokens.Endpoint;
using UpdateTokensEndpoint = WardenId.Protocol.Presentation.Features.Policies.UpdateTokens.Endpoint;

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
        builder.Services.AddSingleton<WardenId.SharedKernel.Application.Abstractions.IAuditActorScope, FakeAuditActorScope>();
        builder.Services.AddSharedKernelPresentation();
        var databaseName = "protocol-" + Guid.NewGuid();
        builder.Services.AddDbContext<ProtocolDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IProtocolDbContext>(sp => sp.GetRequiredService<ProtocolDbContext>());
        builder.Services.AddScoped<IPoliciesQuery, InMemoryPoliciesQuery>();
        builder.Services.AddSingleton<IRefreshTokenRetention, OpenIddictRefreshTokenRetention>();
        builder.Services.Configure<ProtocolOpenIddictPruningOptions>(_ => { });
        builder.Services.AddAuthentication(TestAuthenticationHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.Scheme, null);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        new GetTokensEndpoint().MapEndpoint(app);
        new UpdateTokensEndpoint().MapEndpoint(app);
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
        Assert.Equal(JsonValueKind.Null, root.GetProperty("overrides").GetProperty("accessTokenLifetime").ValueKind);
    }

    private static HttpClient AdminClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.Header, Guid.NewGuid().ToString());
        return client;
    }

    [Fact]
    public async Task Put_StoresOverrides_GetReturnsEffectiveValues_OverridesAndTenantScope()
    {
        await using var app = await StartAsync();
        var client = AdminClient(app);

        var put = await client.PutAsJsonAsync(Path, new
        {
            accessTokenLifetime = "PT5M",
            authorizationCodeLifetime = "PT45S",
            sessionCookieLifetime = "PT1H"
        });

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        using var body = JsonDocument.Parse(await client.GetStringAsync(Path));
        var root = body.RootElement;
        Assert.Equal("tenant", root.GetProperty("scope").GetString());
        Assert.Equal("PT5M", root.GetProperty("accessTokenLifetime").GetString());
        Assert.Equal("PT15M", root.GetProperty("idTokenLifetime").GetString());
        Assert.Equal("PT45S", root.GetProperty("authorizationCodeLifetime").GetString());
        Assert.Equal("PT1H", root.GetProperty("sessionCookieLifetime").GetString());
        Assert.Equal("P1DT12H", root.GetProperty("sessionAbsoluteLifetime").GetString());
        var overrides = root.GetProperty("overrides");
        Assert.Equal("PT5M", overrides.GetProperty("accessTokenLifetime").GetString());
        Assert.Equal(JsonValueKind.Null, overrides.GetProperty("idTokenLifetime").ValueKind);
        Assert.Equal("PT1H", overrides.GetProperty("sessionCookieLifetime").GetString());
    }

    [Fact]
    public async Task Put_AllNull_ClearsBackToGlobal()
    {
        await using var app = await StartAsync();
        var client = AdminClient(app);
        await client.PutAsJsonAsync(Path, new { accessTokenLifetime = "PT5M" });

        var put = await client.PutAsJsonAsync(Path, new { });

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        using var body = JsonDocument.Parse(await client.GetStringAsync(Path));
        Assert.Equal("global", body.RootElement.GetProperty("scope").GetString());
        Assert.Equal("PT10M", body.RootElement.GetProperty("accessTokenLifetime").GetString());
    }

    [Theory]
    [InlineData("accessTokenLifetime", "PT30S")]
    [InlineData("accessTokenLifetime", "fifteen minutes")]
    [InlineData("refreshTokenLifetime", "P1M")]
    [InlineData("authorizationCodeLifetime", "PT11M")]
    [InlineData("sessionCookieLifetime", "P2D")]
    [InlineData("refreshTokenAbsoluteLifetime", "P180D")]
    public async Task Put_Invalid_Returns400_AndChangesNothing(string field, string value)
    {
        await using var app = await StartAsync();
        var client = AdminClient(app);

        var put = await client.PutAsJsonAsync(Path, new Dictionary<string, string> { [field] = value });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Contains(field, await put.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await client.GetStringAsync(Path));
        Assert.Equal("global", body.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Put_Returns401_WithoutAnAuthenticatedCaller()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PutAsJsonAsync(Path, new { accessTokenLifetime = "PT5M" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
