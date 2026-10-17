using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Infrastructure.OpenIddict;
using UpdateTokensCommand = WardenId.Protocol.Application.Features.Policies.UpdateTokens.Command;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>
/// Per-tenant lifetimes (W21) at every issuance point: client override &gt; tenant override &gt; global default.
/// </summary>
public class TenantLifetimeFlowTests
{
    private static async Task SetTenantLifetimesAsync(
        ProtocolTestHost host, string slug = "acme",
        TimeSpan? accessToken = null, TimeSpan? idToken = null, TimeSpan? refreshToken = null,
        TimeSpan? refreshTokenAbsolute = null, TimeSpan? authorizationCode = null,
        TimeSpan? sessionCookie = null, TimeSpan? sessionAbsolute = null)
    {
        var result = await host.SendAsync(new UpdateTokensCommand(
            host.Directory.Tenant(slug).TenantId, accessToken, idToken, refreshToken, refreshTokenAbsolute,
            authorizationCode, sessionCookie, sessionAbsolute, Guid.NewGuid()));
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    private static TimeSpan Lifetime(string jwt)
    {
        var token = new JsonWebTokenHandler().ReadJsonWebToken(jwt);
        return token.ValidTo - token.IssuedAt;
    }

    [Fact]
    public async Task ClientCredentials_UsesTenantAccessLifetime_ButClientOverrideWins()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        await SetTenantLifetimesAsync(host, accessToken: TimeSpan.FromMinutes(5));

        var tenantDefault = await TokenFlow.ClientCredentialsAsync(host, clientId: "svc");
        var clientOverride = await TokenFlow.ClientCredentialsAsync(host, clientId: "short");

        Assert.Equal(TimeSpan.FromMinutes(5), Lifetime(tenantDefault.GetProperty("access_token").GetString()!));
        Assert.Equal(TimeSpan.FromSeconds(60), Lifetime(clientOverride.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task OtherTenants_KeepTheGlobalDefaults()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        await SetTenantLifetimesAsync(host, slug: "globex", accessToken: TimeSpan.FromMinutes(5));

        var acme = await TokenFlow.ClientCredentialsAsync(host, clientId: "svc");

        Assert.Equal(TimeSpan.FromMinutes(15), Lifetime(acme.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task CodeFlow_UsesTenantAccessIdAndRefreshLifetimes()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        await SetTenantLifetimesAsync(host,
            accessToken: TimeSpan.FromMinutes(5), idToken: TimeSpan.FromMinutes(3), refreshToken: TimeSpan.FromDays(2));

        var tokens = await TokenFlow.CodeTokensAsync(host);

        Assert.Equal(TimeSpan.FromMinutes(5), Lifetime(tokens.GetProperty("access_token").GetString()!));
        Assert.Equal(TimeSpan.FromMinutes(3), Lifetime(tokens.GetProperty("id_token").GetString()!));
        using var scope = host.App.Services.CreateScope();
        var refresh = await scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>()
            .Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .SingleAsync(t => t.Type == OpenIddictConstants.TokenTypeIdentifiers.RefreshToken);
        Assert.Equal(TimeSpan.FromDays(2), refresh.ExpirationDate - refresh.CreationDate);

        // Refresh redemption goes through the same factory.
        var refreshed = await TokenFlow.JsonAsync(
            await TokenFlow.RefreshAsync(host, tokens.GetProperty("refresh_token").GetString()!));
        Assert.Equal(TimeSpan.FromMinutes(5), Lifetime(refreshed.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task CodeFlow_ClientIdTokenOverride_WinsOverTenant()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        await SetTenantLifetimesAsync(host, accessToken: TimeSpan.FromMinutes(5), idToken: TimeSpan.FromMinutes(3));

        var tokens = await TokenFlow.CodeTokensAsync(host, clientId: "web-short-id", scope: "openid");

        Assert.Equal(TimeSpan.FromSeconds(120), Lifetime(tokens.GetProperty("id_token").GetString()!));
        Assert.Equal(TimeSpan.FromMinutes(5), Lifetime(tokens.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task AuthorizationCode_ExpiresAfterTheTenantCodeLifetime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await ProtocolTestHost.StartAsync(clock);
        await SetTenantLifetimesAsync(host, authorizationCode: TimeSpan.FromSeconds(30));
        var pkce = OidcFlow.NewPkce();
        var code = await OidcFlow.CodeAsync(host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, pkce, "openid"), FakeDirectory.WebRedirect);

        // Inside the 5-minute global default, past the tenant's 30 seconds.
        clock.Advance(TimeSpan.FromSeconds(45));
        var response = await host.Client.PostAsync("/tenants/acme/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "web",
            ["client_secret"] = FakeDirectory.AcmeSecret,
            ["code"] = code,
            ["redirect_uri"] = FakeDirectory.WebRedirect,
            ["code_verifier"] = pkce.Verifier
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_grant", (await TokenFlow.JsonAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Session_UsesTheTenantAbsoluteLifetime()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        await SetTenantLifetimesAsync(host, sessionCookie: TimeSpan.FromMinutes(30), sessionAbsolute: TimeSpan.FromHours(2));

        await TokenFlow.CodeTokensAsync(host);

        var session = Assert.Single(await host.SessionsOfAsync(host.Directory.User("alice").UserId));
        Assert.Equal(session.AuthTime + TimeSpan.FromHours(2), session.ExpiresAt);
    }

    [Fact]
    public async Task SessionCookie_IdlesOutAfterTheTenantCookieLifetime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await ProtocolTestHost.StartAsync(clock);
        await SetTenantLifetimesAsync(host, sessionCookie: TimeSpan.FromMinutes(10));
        var browser = host.CreateBrowser();
        var parameters = OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, OidcFlow.NewPkce(), "openid");
        await OidcFlow.CodeAsync(browser, "acme", parameters, FakeDirectory.WebRedirect);

        // Still inside 10 minutes: silent re-authorize works.
        clock.Advance(TimeSpan.FromMinutes(9));
        OidcFlow.RedirectQuery(await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", parameters)), FakeDirectory.WebRedirect);

        // Idle for longer than the tenant cookie lifetime (well inside the 8h global one).
        clock.Advance(TimeSpan.FromMinutes(11));
        var response = await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", parameters));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/tenants/acme/login?ReturnUrl=", response.Headers.Location!.OriginalString);
    }
}
