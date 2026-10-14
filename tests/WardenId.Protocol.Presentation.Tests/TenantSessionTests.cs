using System.Net;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Domain.Sessions;
using EndSessionCommand = WardenId.Protocol.Application.Features.Sessions.End.Command;

namespace WardenId.Protocol.Presentation.Tests;

public class TenantSessionTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<Cookie> LoginAsync()
    {
        var browser = _host.CreateBrowser();
        var loginUrl = "/tenants/acme/login";
        var page = await browser.GetAsync(loginUrl);
        var token = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = "alice",
            ["Password"] = FakeDirectory.UserPassword,
            ["__RequestVerificationToken"] = token
        });
        return browser.Cookies.GetAllCookies().Single(c => c.Name == TenantSession.Scheme);
    }

    // A fresh request per read: the cookie handler caches its ticket per request.
    private async Task<(TenantSessionInfo? Info, HttpContext Context)> ReadAsync(Cookie cookie, string path, Guid tenantId)
    {
        using var scope = _host.App.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = path;
        context.Request.Headers.Cookie = $"{cookie.Name}={cookie.Value}";
        var info = await TenantSession.ReadAsync(
            context, tenantId, scope.ServiceProvider.GetRequiredService<ISender>(), CancellationToken.None);
        return (info, context);
    }

    [Fact]
    public async Task ReadAsync_RejectsTicketIssuedForAnotherTenant_EvenWhenCookieIsSent()
    {
        var cookie = await LoginAsync();
        var acme = _host.Directory.Tenant("acme").TenantId;
        var globex = _host.Directory.Tenant("globex").TenantId;

        Assert.Null((await ReadAsync(cookie, "/tenants/globex/x", globex)).Info);
        var info = (await ReadAsync(cookie, "/tenants/acme/x", acme)).Info;
        Assert.NotNull(info);
        Assert.Equal(_host.Directory.User("alice").UserId, info.UserId);
    }

    [Fact]
    public async Task Login_StartsSessionRow_AndCookieReferencesIt()
    {
        var cookie = await LoginAsync();
        var alice = _host.Directory.User("alice");

        var info = (await ReadAsync(cookie, "/tenants/acme/x", _host.Directory.Tenant("acme").TenantId)).Info;

        var session = Assert.Single(await _host.SessionsOfAsync(alice.UserId));
        Assert.NotNull(info);
        Assert.Equal(session.Id, info.Sid);
        Assert.Equal(session.AuthTime, info.AuthTime);
        Assert.Equal(_host.Directory.Tenant("acme").TenantId, session.TenantId);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public async Task ReadAsync_SessionRowEnded_ReturnsNull_AndSignsCookieOut()
    {
        var cookie = await LoginAsync();
        var acme = _host.Directory.Tenant("acme").TenantId;
        var session = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));
        Assert.True((await _host.SendAsync(new EndSessionCommand(acme, session.Id, EndReason.AdminEnded, RequireExisting: true))).IsSuccess);

        var (info, context) = await ReadAsync(cookie, "/tenants/acme/x", acme);

        Assert.Null(info);
        var setCookie = context.Response.Headers.SetCookie.Single(c => c!.StartsWith(TenantSession.Scheme + "="))!;
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CookieScheme_UsesConfiguredLifetime_AndSlides()
    {
        var options = _host.App.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(TenantSession.Scheme);

        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }
}
