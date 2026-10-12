using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WardenId.Protocol.Presentation.Tests;

public class TenantSessionTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task ReadAsync_RejectsTicketIssuedForAnotherTenant_EvenWhenCookieIsSent()
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
        var cookie = browser.Cookies.GetAllCookies().Single(c => c.Name == TenantSession.Scheme);

        using var scope = _host.App.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = "/tenants/globex/x";
        context.Request.Headers.Cookie = $"{cookie.Name}={cookie.Value}";

        var acme = _host.Directory.Tenant("acme").TenantId;
        var globex = _host.Directory.Tenant("globex").TenantId;

        Assert.Null(await TenantSession.ReadAsync(context, globex));
        var info = await TenantSession.ReadAsync(context, acme);
        Assert.NotNull(info);
        Assert.Equal(_host.Directory.User("alice").UserId, info.UserId);
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
