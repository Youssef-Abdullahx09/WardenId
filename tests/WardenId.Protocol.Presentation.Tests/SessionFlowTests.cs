using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Application;
using WardenId.Protocol.Domain.Sessions;
using WardenId.Protocol.Infrastructure.OpenIddict;
using EndSessionCommand = WardenId.Protocol.Application.Features.Sessions.End.Command;

namespace WardenId.Protocol.Presentation.Tests;

public class SessionFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static Dictionary<string, string> Authorize(string clientId = "web", string scope = "openid profile email") =>
        OidcFlow.AuthorizeParameters(clientId, FakeDirectory.WebRedirect, OidcFlow.NewPkce(), scope);

    private static void AssertLoginRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/tenants/acme/login?ReturnUrl=", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task IdToken_CarriesSidOfTheSessionRow_AccessTokenDoesNot()
    {
        var tokens = await TokenFlow.CodeTokensAsync(_host);

        var session = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));
        var handler = new JsonWebTokenHandler();
        var id = handler.ReadJsonWebToken(tokens.GetProperty("id_token").GetString()!);
        Assert.Equal(session.Id.ToString(), id.GetClaim(SessionClaimTypes.SessionId).Value);
        // The row, not the cookie, is the source of auth_time.
        Assert.Equal(session.AuthTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), id.GetClaim("auth_time").Value);
        var access = handler.ReadJsonWebToken(tokens.GetProperty("access_token").GetString()!);
        Assert.False(access.TryGetClaim(SessionClaimTypes.SessionId, out _));

        // ID tokens re-issued on refresh keep the sid persisted in the refresh token principal.
        var refreshed = await TokenFlow.JsonAsync(
            await TokenFlow.RefreshAsync(_host, tokens.GetProperty("refresh_token").GetString()!));
        var refreshedId = handler.ReadJsonWebToken(refreshed.GetProperty("id_token").GetString()!);
        Assert.Equal(session.Id.ToString(), refreshedId.GetClaim(SessionClaimTypes.SessionId).Value);
    }

    [Fact]
    public async Task Authorize_RecordsEachAuthorizationOnTheSession_WithOfflineAccessFlag()
    {
        var browser = _host.CreateBrowser();
        await OidcFlow.CodeAsync(browser, "acme", Authorize("web-rt", "openid offline_access"), FakeDirectory.WebRedirect);
        await OidcFlow.CodeAsync(browser, "acme", Authorize("web", "openid"), FakeDirectory.WebRedirect);

        var session = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));
        Assert.Equal(2, session.Authorizations.Count);
        Assert.True(session.Authorizations.Single(a => a.ClientId == "web-rt").OfflineAccess);
        Assert.False(session.Authorizations.Single(a => a.ClientId == "web").OfflineAccess);

        // The recorded ids are OpenIddict's ad-hoc authorizations — the handle revocation uses.
        using var scope = _host.App.Services.CreateScope();
        var authorizationIds = await scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>()
            .Set<OpenIddictEntityFrameworkCoreAuthorization>().Select(a => a.Id).ToListAsync();
        Assert.All(session.Authorizations, a => Assert.Contains(a.AuthorizationId, authorizationIds));
    }

    [Fact]
    public async Task Authorize_WithEndedSessionRow_RedirectsToLogin_AndClearsCookie()
    {
        var browser = _host.CreateBrowser();
        await OidcFlow.CodeAsync(browser, "acme", Authorize(), FakeDirectory.WebRedirect);
        var session = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));
        await _host.SendAsync(new EndSessionCommand(
            _host.Directory.Tenant("acme").TenantId, session.Id, EndReason.AdminEnded, RequireExisting: true));

        var response = await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", Authorize()));

        AssertLoginRedirect(response);
        Assert.DoesNotContain(browser.Cookies.GetAllCookies(), c => c.Name == TenantSession.Scheme);
    }

    [Fact]
    public async Task Authorize_AfterAbsoluteLifetime_RedirectsToLogin()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await ProtocolTestHost.StartAsync(clock, new Dictionary<string, string?>
        {
            ["ProtocolSession:AbsoluteLifetime"] = "01:00:00"
        });
        var browser = host.CreateBrowser();
        await OidcFlow.CodeAsync(browser, "acme", Authorize(), FakeDirectory.WebRedirect);

        // Well inside the 8h sliding cookie, past the 1h absolute cap of the row.
        clock.Advance(TimeSpan.FromMinutes(61));
        var response = await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", Authorize()));

        AssertLoginRedirect(response);
    }
}
