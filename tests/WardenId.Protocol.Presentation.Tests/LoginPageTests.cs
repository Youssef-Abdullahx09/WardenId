using System.Net;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Presentation.Tests;

public class LoginPageTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private const string ReturnUrl = "/tenants/acme/authorize?client_id=web&state=xyz";

    private static async Task<HttpResponseMessage> PostLoginAsync(
        BrowserClient browser, string slug, string user, string password, string? returnUrl = ReturnUrl)
    {
        var loginUrl = $"/tenants/{slug}/login" + (returnUrl is null ? "" : $"?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        var page = await browser.GetAsync(loginUrl);
        var token = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        return await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = user,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl ?? "",
            ["__RequestVerificationToken"] = token
        });
    }

    private static bool HasSessionCookie(BrowserClient browser) =>
        browser.Cookies.GetAllCookies().Any(c => c.Name == TenantSession.Scheme);

    [Fact]
    public async Task Get_RendersForm_WithSecurityHeaders()
    {
        var response = await _host.CreateBrowser().GetAsync($"/tenants/acme/login?ReturnUrl={Uri.EscapeDataString(ReturnUrl)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"EmailOrUsername\"", html);
        Assert.Contains("name=\"cancel\"", html);
    }

    [Theory]
    [InlineData("initech")]
    [InlineData("frozen")]
    [InlineData("ACME")]
    public async Task Get_UnknownSuspendedOrMixedCaseTenant_Returns404(string slug) =>
        Assert.Equal(HttpStatusCode.NotFound, (await _host.CreateBrowser().GetAsync($"/tenants/{slug}/login")).StatusCode);

    [Fact]
    public async Task Post_ValidCredentials_SetsTenantScopedCookie_AndRedirectsToReturnUrl()
    {
        var browser = _host.CreateBrowser();

        var response = await PostLoginAsync(browser, "acme", "alice", FakeDirectory.UserPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ReturnUrl, response.Headers.Location!.OriginalString);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(TenantSession.Scheme + "="));
        Assert.Contains("path=/tenants/acme", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("alice", "wrong")]
    [InlineData("nobody", FakeDirectory.UserPassword)]
    [InlineData("carol", FakeDirectory.UserPassword)]
    public async Task Post_BadOrUnusableCredentials_ShowGenericError_NoCookie(string user, string password)
    {
        var browser = _host.CreateBrowser();

        var response = await PostLoginAsync(browser, "acme", user, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid username or password.", await response.Content.ReadAsStringAsync());
        Assert.False(HasSessionCookie(browser));
    }

    [Fact]
    public async Task Post_BlockedUser_ShowsReason_AndBackButton_NoCookie()
    {
        var browser = _host.CreateBrowser();

        var response = await PostLoginAsync(browser, "acme", "bob", FakeDirectory.UserPassword);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("verify your email address", html);
        Assert.Contains("Back to application", html);
        Assert.False(HasSessionCookie(browser));
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("/tenants/globex/authorize?client_id=web")]
    public async Task Post_DisallowedReturnUrl_DoesNotRedirect(string returnUrl)
    {
        var browser = _host.CreateBrowser();

        var response = await PostLoginAsync(browser, "acme", "alice", FakeDirectory.UserPassword, returnUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You are signed in", await response.Content.ReadAsStringAsync());
        Assert.True(HasSessionCookie(browser));
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_Returns400()
    {
        var response = await _host.CreateBrowser().PostFormAsync("/tenants/acme/login", new Dictionary<string, string>
        {
            ["EmailOrUsername"] = "alice",
            ["Password"] = FakeDirectory.UserPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_SessionStartFails_ShowsGenericError_NoCookie()
    {
        // A zero absolute lifetime makes Session.Start fail (expiresAt <= authTime). The options validator
        // lives in AddProtocolInfrastructure, which this host does not call.
        await using var host = await ProtocolTestHost.StartAsync(settings: new Dictionary<string, string?>
        {
            ["ProtocolSession:AbsoluteLifetime"] = "00:00:00"
        });
        var browser = host.CreateBrowser();

        var response = await PostLoginAsync(browser, "acme", "alice", FakeDirectory.UserPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign-in failed. Please try again.", await response.Content.ReadAsStringAsync());
        Assert.False(HasSessionCookie(browser));
        Assert.Empty(await host.SessionsOfAsync(host.Directory.User("alice").UserId));
    }

    [Fact]
    public async Task Login_AgainAsSameUser_ReusesSession()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "acme", "alice", FakeDirectory.UserPassword);
        var first = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));

        var response = await PostLoginAsync(browser, "acme", "alice", FakeDirectory.UserPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var only = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));
        Assert.Equal(first.Id, only.Id);
        Assert.Null(only.EndedAt);
        // The re-issued cookie still names that session: an authorization is recorded on it.
        await OidcFlow.CodeAsync(browser, "acme",
            OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, OidcFlow.NewPkce(), "openid"), FakeDirectory.WebRedirect);
        var used = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));
        Assert.Single(used.Authorizations);
    }

    [Fact]
    public async Task Login_AsDifferentUser_EndsPreviousSession()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "acme", "alice", FakeDirectory.UserPassword);
        var alice = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("alice").UserId));

        await PostLoginAsync(browser, "acme", "dave", FakeDirectory.UserPassword);

        var ended = (await _host.FindSessionAsync(alice.Id))!;
        Assert.NotNull(ended.EndedAt);
        Assert.Equal(EndReason.UserLogout, ended.EndReason);
        var dave = Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("dave").UserId));
        Assert.Null(dave.EndedAt);
    }
}
