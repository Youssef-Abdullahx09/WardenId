using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;

namespace WardenId.Protocol.Presentation.Tests;

public class AuthorizeEndpointTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static Dictionary<string, string> Web(OidcFlow.Pkce? pkce = null, string scope = "openid profile email") =>
        OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, pkce, scope);

    [Fact]
    public async Task NoSession_RedirectsToTenantLogin_WithAuthorizeReturnUrl()
    {
        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl("acme", Web(OidcFlow.NewPkce())));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith("/tenants/acme/login?ReturnUrl=", location);
        var returnUrl = QueryHelpers.ParseQuery(new Uri("https://x" + location).Query)["ReturnUrl"].ToString();
        Assert.StartsWith("/tenants/acme/authorize?", returnUrl);
        Assert.Contains("client_id=web", returnUrl);
    }

    [Fact]
    public async Task AfterLogin_RedirectsToClient_WithCodeStateAndIss()
    {
        var response = await OidcFlow.AuthorizeWithLoginAsync(_host.CreateBrowser(), "acme", Web(OidcFlow.NewPkce()));

        var query = OidcFlow.RedirectQuery(response, FakeDirectory.WebRedirect);
        Assert.False(string.IsNullOrEmpty(query["code"]));
        Assert.Equal("xyz", query["state"]);
        Assert.Equal("https://wardenid.test/tenants/acme", query["iss"]);
    }

    [Fact]
    public async Task ExistingSession_SkipsLogin()
    {
        var browser = _host.CreateBrowser();
        await OidcFlow.CodeAsync(browser, "acme", Web(OidcFlow.NewPkce()), FakeDirectory.WebRedirect);

        var second = await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", Web(OidcFlow.NewPkce())));

        Assert.True(OidcFlow.RedirectQuery(second, FakeDirectory.WebRedirect).ContainsKey("code"));
    }

    private async Task<(BrowserClient Browser, HttpResponseMessage Response)> AuthorizeAfterSessionBecomesUnusableAsync(
        string? prompt, Action<FakeDirectory.UserEntry> breakUser)
    {
        var browser = _host.CreateBrowser();
        await OidcFlow.CodeAsync(browser, "acme", Web(OidcFlow.NewPkce()), FakeDirectory.WebRedirect);
        breakUser(_host.Directory.User("alice"));

        var parameters = Web(OidcFlow.NewPkce());
        if (prompt is not null) parameters["prompt"] = prompt;
        return (browser, await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", parameters)));
    }

    private static void AssertSessionCookieCleared(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(TenantSession.Scheme + "="));
        Assert.Contains("path=/tenants/acme", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("pending-action")]
    [InlineData("disabled")]
    public async Task SessionUserNoLongerUsable_SignsOutAndRedirectsToLogin(string how)
    {
        var (browser, response) = await AuthorizeAfterSessionBecomesUnusableAsync(null, u =>
        {
            if (how == "disabled") u.Status = UserStatus.Disabled;
            else u.PendingActions.Add(RequiredAction.ResetPassword);
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/tenants/acme/login?ReturnUrl=", response.Headers.Location!.OriginalString);
        AssertSessionCookieCleared(response);
        Assert.DoesNotContain(browser.Cookies.GetAllCookies(), c => c.Name == TenantSession.Scheme);
    }

    [Fact]
    public async Task SessionUserNoLongerUsable_PromptNone_IsLoginRequired()
    {
        var (_, response) = await AuthorizeAfterSessionBecomesUnusableAsync(
            "none", u => u.PendingActions.Add(RequiredAction.ResetPassword));

        Assert.Equal("login_required", OidcFlow.RedirectQuery(response, FakeDirectory.WebRedirect)["error"]);
        AssertSessionCookieCleared(response);
    }

    // Review focus: OIDC allows POSTed authorize requests; login must resume them.
    [Fact]
    public async Task PostedAuthorizeRequest_ResumesAfterLogin()
    {
        var browser = _host.CreateBrowser();
        var parameters = Web(OidcFlow.NewPkce());

        var start = await browser.PostFormAsync("/tenants/acme/authorize", parameters);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var returnUrl = QueryHelpers.ParseQuery(new Uri("https://x" + start.Headers.Location!.OriginalString).Query)["ReturnUrl"].ToString();

        var query = OidcFlow.RedirectQuery(
            await OidcFlow.AuthorizeWithLoginAsync(browser, "acme",
                QueryHelpers.ParseQuery(new Uri("https://x" + returnUrl).Query).ToDictionary(p => p.Key, p => p.Value.ToString())),
            FakeDirectory.WebRedirect);
        Assert.True(query.ContainsKey("code"));
    }

    [Fact]
    public async Task SessionFromOtherTenant_IsNotHonoured()
    {
        var browser = _host.CreateBrowser();
        await OidcFlow.CodeAsync(browser, "acme", Web(OidcFlow.NewPkce()), FakeDirectory.WebRedirect);

        // The jar honours cookie Path, so send the acme session cookie to globex explicitly.
        var cookie = browser.Cookies.GetAllCookies().Single(c => c.Name == TenantSession.Scheme);
        var request = new HttpRequestMessage(HttpMethod.Get, OidcFlow.AuthorizeUrl("globex", Web(OidcFlow.NewPkce())));
        request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
        var response = await browser.SendAsync(request);

        Assert.StartsWith("/tenants/globex/login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task PromptNone_WithoutSession_IsLoginRequired()
    {
        var parameters = Web(OidcFlow.NewPkce());
        parameters["prompt"] = "none";

        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl("acme", parameters));

        var query = OidcFlow.RedirectQuery(response, FakeDirectory.WebRedirect);
        Assert.Equal("login_required", query["error"]);
        Assert.Equal("xyz", query["state"]);
    }

    [Fact]
    public async Task Cancel_FromLoginPage_IsAccessDenied()
    {
        var browser = _host.CreateBrowser();
        var start = await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", Web(OidcFlow.NewPkce())));
        var page = await browser.GetAsync(start.Headers.Location!.OriginalString);
        var html = await page.Content.ReadAsStringAsync();
        var cancelForm = html[html.IndexOf("action=\"/tenants/acme/authorize\"", StringComparison.Ordinal)..];
        var token = BrowserClient.ReadAntiforgeryToken(cancelForm);

        var form = Web(OidcFlow.NewPkce());
        form["cancel"] = "true";
        form["__RequestVerificationToken"] = token;
        var response = await browser.PostFormAsync("/tenants/acme/authorize", form);

        var query = OidcFlow.RedirectQuery(response, FakeDirectory.WebRedirect);
        Assert.Equal("access_denied", query["error"]);
        Assert.Equal("xyz", query["state"]);
    }

    [Fact]
    public async Task Cancel_WithoutAntiforgeryToken_Returns400()
    {
        var form = Web(OidcFlow.NewPkce());
        form["cancel"] = "true";

        var response = await _host.CreateBrowser().PostFormAsync("/tenants/acme/authorize", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task BlockedUser_BackToApplication_IsAccessDenied()
    {
        var browser = _host.CreateBrowser();
        var start = await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", Web(OidcFlow.NewPkce())));
        var loginUrl = start.Headers.Location!.OriginalString;
        var page = await browser.GetAsync(loginUrl);
        var returnUrl = QueryHelpers.ParseQuery(new Uri("https://x" + loginUrl).Query)["ReturnUrl"].ToString();
        var blocked = await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = "bob",
            ["Password"] = FakeDirectory.UserPassword,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync())
        });
        var html = await blocked.Content.ReadAsStringAsync();
        Assert.Contains("Back to application", html);

        var form = QueryHelpers.ParseQuery(new Uri("https://x" + returnUrl).Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        form["cancel"] = "true";
        form["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(html);
        var response = await browser.PostFormAsync("/tenants/acme/authorize", form);

        Assert.Equal("access_denied", OidcFlow.RedirectQuery(response, FakeDirectory.WebRedirect)["error"]);
    }

    [Theory]
    [InlineData("profile email", "invalid_scope")]
    // OpenIddict (ID2035) rejects offline_access itself, locally, when the refresh-token flow is not enabled.
    [InlineData("openid offline_access", "invalid_request")]
    [InlineData("openid api.read", "invalid_scope")]
    public async Task MissingOpenIdOrUnsupportedScope_IsRejected(string scope, string expectedError)
    {
        var response = await OidcFlow.AuthorizeWithLoginAsync(_host.CreateBrowser(), "acme", Web(OidcFlow.NewPkce(), scope));

        Assert.Equal(expectedError, await OidcFlow.AuthorizeErrorAsync(response));
    }

    [Fact]
    public async Task PublicClient_WithoutPkce_IsInvalidRequest()
    {
        var parameters = OidcFlow.AuthorizeParameters("spa", FakeDirectory.SpaRedirect, pkce: null);

        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl("acme", parameters));

        Assert.Equal("invalid_request", await OidcFlow.AuthorizeErrorAsync(response));
    }

    [Fact]
    public async Task PlainPkce_IsRejected()
    {
        var parameters = OidcFlow.AuthorizeParameters("spa", FakeDirectory.SpaRedirect, pkce: null);
        parameters["code_challenge"] = "plain-challenge-plain-challenge-plain-challenge-1234";
        parameters["code_challenge_method"] = "plain";

        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl("acme", parameters));

        Assert.Equal("invalid_request", await OidcFlow.AuthorizeErrorAsync(response));
    }

    [Theory]
    [InlineData("web", "https://evil.example/cb")]
    [InlineData("web", "https://app.example/cb/extra")]
    [InlineData("nope", FakeDirectory.WebRedirect)]
    public async Task UntrustedClientOrRedirectUri_NeverRedirects(string clientId, string redirectUri)
    {
        var parameters = OidcFlow.AuthorizeParameters(clientId, redirectUri, OidcFlow.NewPkce());

        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl("acme", parameters));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ClientWithoutAuthorizationCodeGrant_IsRejected()
    {
        // "svc" is client_credentials-only and has no registered redirect URI, so OpenIddict can't redirect.
        var parameters = OidcFlow.AuthorizeParameters("svc", FakeDirectory.WebRedirect, OidcFlow.NewPkce());

        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl("acme", parameters));

        Assert.Null(response.Headers.Location);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("initech", HttpStatusCode.NotFound)]
    [InlineData("ACME", HttpStatusCode.NotFound)]
    public async Task UnknownOrMixedCaseTenant_Returns404(string slug, HttpStatusCode expected)
    {
        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl(slug, Web(OidcFlow.NewPkce())));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task SuspendedTenant_DoesNotRedirect()
    {
        var response = await _host.CreateBrowser().GetAsync(OidcFlow.AuthorizeUrl("frozen", Web(OidcFlow.NewPkce())));

        Assert.Null(response.Headers.Location);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
