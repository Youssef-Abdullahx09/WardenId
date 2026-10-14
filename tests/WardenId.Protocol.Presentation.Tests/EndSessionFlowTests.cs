using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Protocol.Application;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Presentation.Tests;

public partial class EndSessionFlowTests : IAsyncLifetime
{
    private const string LoggedOut = FakeDirectory.WebLoggedOut;
    private const string State = "st-1";

    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private sealed record SignedIn(BrowserClient Browser, string IdToken, Guid Sid);

    private sealed record Confirmation(string AntiforgeryToken, Dictionary<string, string> Fields, string Html);

    /// <summary>alice signs in to web-rt in a fresh browser; the cookie stays in that browser.</summary>
    private static async Task<SignedIn> SignInAsync(ProtocolTestHost host)
    {
        var browser = host.CreateBrowser();
        var tokens = await TokenFlow.CodeTokensAsync(host, browser: browser);
        var idToken = tokens.GetProperty("id_token").GetString()!;
        var sid = Guid.Parse(new JsonWebTokenHandler().ReadJsonWebToken(idToken).GetClaim(SessionClaimTypes.SessionId).Value);
        return new SignedIn(browser, idToken, sid);
    }

    private static string EndSessionUrl(
        string? hint, string? clientId = "web-rt", string? postLogoutRedirectUri = LoggedOut, string? state = State,
        string slug = "acme", IReadOnlyDictionary<string, string>? extra = null)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["id_token_hint"] = hint,
            ["client_id"] = clientId,
            ["post_logout_redirect_uri"] = postLogoutRedirectUri,
            ["state"] = state
        };
        foreach (var (key, value) in extra ?? new Dictionary<string, string>())
            parameters[key] = value;
        return QueryHelpers.AddQueryString($"/tenants/{slug}/endsession", parameters.Where(p => p.Value is not null));
    }

    private static Dictionary<string, string> Authorize(string scope = "openid") =>
        OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, OidcFlow.NewPkce(), scope);

    private static bool HasSessionCookie(BrowserClient browser) =>
        browser.Cookies.GetAllCookies().Any(c => c.Name == TenantSession.Scheme);

    private static void AssertRedirectedToRp(HttpResponseMessage response) =>
        Assert.Equal(State, OidcFlow.RedirectQuery(response, LoggedOut)["state"]);

    private static async Task AssertEndedAsync(ProtocolTestHost host, Guid sid)
    {
        var session = await host.FindSessionAsync(sid);
        Assert.NotNull(session);
        Assert.NotNull(session.EndedAt);
        Assert.Equal(EndReason.UserLogout, session.EndReason);
    }

    private static async Task AssertLiveAsync(ProtocolTestHost host, Guid sid)
    {
        var session = await host.FindSessionAsync(sid);
        Assert.NotNull(session);
        Assert.Null(session.EndedAt);
    }

    private static async Task AssertRejectedLocallyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("invalid_request", await OidcFlow.AuthorizeErrorAsync(response));
    }

    private static async Task<Confirmation> OpenConfirmationAsync(BrowserClient browser, HttpResponseMessage endSession)
    {
        Assert.Equal(HttpStatusCode.Redirect, endSession.StatusCode);
        var location = endSession.Headers.Location!.OriginalString;
        Assert.StartsWith("/tenants/acme/logout", location);

        var page = await browser.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("DENY", page.Headers.GetValues("X-Frame-Options").Single());
        var html = await page.Content.ReadAsStringAsync();
        var fields = HiddenField().Matches(html)
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        return new Confirmation(BrowserClient.ReadAntiforgeryToken(html), fields, html);
    }

    private static Task<HttpResponseMessage> PostConfirmationAsync(BrowserClient browser, Confirmation confirmation, string button)
    {
        var form = new Dictionary<string, string>(confirmation.Fields)
        {
            [button] = "true",
            ["__RequestVerificationToken"] = confirmation.AntiforgeryToken
        };
        return browser.PostFormAsync("/tenants/acme/endsession", form);
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\"")]
    private static partial Regex HiddenField();

    [Fact]
    public async Task MatchingHint_LogsOutImmediately_AndRedirectsToRpWithState()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken));

        AssertRedirectedToRp(response);
        Assert.False(HasSessionCookie(alice.Browser));
        await AssertEndedAsync(_host, alice.Sid);
        var authorize = await alice.Browser.GetAsync(OidcFlow.AuthorizeUrl("acme", Authorize()));
        Assert.StartsWith("/tenants/acme/login", authorize.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ReplayedCookieAfterLogout_IsNoSession()
    {
        var alice = await SignInAsync(_host);
        var cookie = alice.Browser.Cookies.GetAllCookies().Single(c => c.Name == TenantSession.Scheme);
        AssertRedirectedToRp(await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken)));

        // The cookie is still cryptographically valid; only the ended row makes it worthless.
        var request = new HttpRequestMessage(HttpMethod.Get, OidcFlow.AuthorizeUrl("acme", Authorize()));
        request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
        var response = await _host.CreateBrowser().SendAsync(request);

        Assert.StartsWith("/tenants/acme/login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task WithoutHint_RedirectsToConfirmationPage_WithWhitelistedParameters()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(
            hint: null, extra: new Dictionary<string, string> { ["ui_locales"] = "en", ["foo"] = "bar" }));

        Assert.DoesNotContain("foo=", response.Headers.Location!.OriginalString);
        var confirmation = await OpenConfirmationAsync(alice.Browser, response);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["client_id"] = "web-rt",
                ["post_logout_redirect_uri"] = LoggedOut,
                ["state"] = State,
                ["ui_locales"] = "en"
            }.OrderBy(p => p.Key),
            confirmation.Fields.OrderBy(p => p.Key));
        Assert.Contains("name=\"confirm\"", confirmation.Html);
        Assert.Contains("name=\"cancel\"", confirmation.Html);
        Assert.True(HasSessionCookie(alice.Browser));
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task ConfirmPost_LogsOut_AndRedirectsToRpWithState()
    {
        var alice = await SignInAsync(_host);
        var confirmation = await OpenConfirmationAsync(alice.Browser, await alice.Browser.GetAsync(EndSessionUrl(hint: null)));

        var response = await PostConfirmationAsync(alice.Browser, confirmation, "confirm");

        AssertRedirectedToRp(response);
        Assert.False(HasSessionCookie(alice.Browser));
        await AssertEndedAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task ConfirmPost_WithoutAntiforgeryToken_Returns400_AndKeepsSession()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.PostFormAsync("/tenants/acme/endsession", new Dictionary<string, string>
        {
            ["client_id"] = "web-rt",
            ["post_logout_redirect_uri"] = LoggedOut,
            ["state"] = State,
            ["confirm"] = "true"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(HasSessionCookie(alice.Browser));
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task ConfirmOnGet_IsIgnored_AndStillAsksForConfirmation()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(
            hint: null, extra: new Dictionary<string, string> { ["confirm"] = "true" }));

        await OpenConfirmationAsync(alice.Browser, response);
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task CancelPost_RedirectsToRpWithState_AndKeepsSession()
    {
        var alice = await SignInAsync(_host);
        var confirmation = await OpenConfirmationAsync(alice.Browser, await alice.Browser.GetAsync(EndSessionUrl(hint: null)));

        var response = await PostConfirmationAsync(alice.Browser, confirmation, "cancel");

        AssertRedirectedToRp(response);
        Assert.True(HasSessionCookie(alice.Browser));
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task CancelPost_WithoutPostLogoutRedirectUri_ShowsStillSignedIn()
    {
        var alice = await SignInAsync(_host);
        var confirmation = await OpenConfirmationAsync(alice.Browser, await alice.Browser.GetAsync(
            EndSessionUrl(hint: null, clientId: null, postLogoutRedirectUri: null, state: null)));

        var response = await PostConfirmationAsync(alice.Browser, confirmation, "cancel");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/tenants/acme/logout?cancelled=true", response.Headers.Location!.OriginalString);
        var page = await alice.Browser.GetAsync(response.Headers.Location.OriginalString);
        Assert.Contains("You are still signed in.", await page.Content.ReadAsStringAsync());
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task HintForAnotherSession_RedirectsToConfirmationPage()
    {
        var browserA = await SignInAsync(_host);
        var browserB = await SignInAsync(_host);
        Assert.NotEqual(browserA.Sid, browserB.Sid);

        var response = await browserA.Browser.GetAsync(EndSessionUrl(browserB.IdToken));

        await OpenConfirmationAsync(browserA.Browser, response);
        await AssertLiveAsync(_host, browserA.Sid);
        await AssertLiveAsync(_host, browserB.Sid);
    }

    // Review focus: RP logout from browser A must leave the same user's browser B signed in.
    [Fact]
    public async Task Logout_EndsOnlyThisBrowsersSession()
    {
        var browserA = await SignInAsync(_host);
        var browserB = await SignInAsync(_host);

        AssertRedirectedToRp(await browserA.Browser.GetAsync(EndSessionUrl(browserA.IdToken)));

        await AssertEndedAsync(_host, browserA.Sid);
        await AssertLiveAsync(_host, browserB.Sid);
        var silent = Authorize();
        silent["prompt"] = "none";
        var authorize = await browserB.Browser.GetAsync(OidcFlow.AuthorizeUrl("acme", silent));
        Assert.True(OidcFlow.RedirectQuery(authorize, FakeDirectory.WebRedirect).ContainsKey("code"));
    }

    // Review focus: RPs keep stale ID tokens; an expired hint must still log out without confirmation.
    [Fact]
    public async Task ExpiredHint_StillLogsOutImmediately()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await ProtocolTestHost.StartAsync(clock);
        var alice = await SignInAsync(host);

        clock.Advance(TimeSpan.FromMinutes(16)); // IdTokenLifetime is 15 minutes
        Assert.True(new JsonWebTokenHandler().ReadJsonWebToken(alice.IdToken).ValidTo < clock.GetUtcNow().UtcDateTime);

        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken));

        AssertRedirectedToRp(response);
        await AssertEndedAsync(host, alice.Sid);
    }

    [Theory]
    [InlineData("https://evil.example/logged-out")]
    [InlineData(FakeDirectory.WebRedirect)] // a registered redirect_uri, not a post-logout one
    public async Task UnregisteredPostLogoutRedirectUri_Returns400_WithoutRedirect(string uri)
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken, postLogoutRedirectUri: uri));

        await AssertRejectedLocallyAsync(response);
        Assert.True(HasSessionCookie(alice.Browser));
        await AssertLiveAsync(_host, alice.Sid);
    }

    // Review focus: exact match only — an extra query on a registered URI is a different URI.
    [Fact]
    public async Task PostLogoutRedirectUri_WithExtraQuery_IsRejected()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken, postLogoutRedirectUri: LoggedOut + "?next=/admin"));

        await AssertRejectedLocallyAsync(response);
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task PostLogoutRedirectUri_WithoutClientIdOrHint_Returns400()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(hint: null, clientId: null));

        await AssertRejectedLocallyAsync(response);
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task HintWithoutClientId_InfersClient_AndRedirectsToRp()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken, clientId: null));

        AssertRedirectedToRp(response);
        await AssertEndedAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task HintIssuedToAnotherClient_Returns400()
    {
        var alice = await SignInAsync(_host); // ID token for web-rt

        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken, clientId: "web", postLogoutRedirectUri: null));

        await AssertRejectedLocallyAsync(response);
        await AssertLiveAsync(_host, alice.Sid);
    }

    // A3: a hint signed by another tenant's key is dropped by OpenIddict → treated as no hint.
    [Fact]
    public async Task HintFromAnotherTenant_IsIgnored_AndAsksForConfirmation()
    {
        var alice = await SignInAsync(_host);
        var foreignHint = await GlobexIdTokenAsync();

        var response = await alice.Browser.GetAsync(EndSessionUrl(foreignHint));

        await OpenConfirmationAsync(alice.Browser, response);
        await AssertLiveAsync(_host, alice.Sid);
    }

    [Fact]
    public async Task ValidHint_WithoutLiveSession_StillRedirectsToRp()
    {
        var alice = await SignInAsync(_host);
        AssertRedirectedToRp(await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken)));

        // RP retry / double click: nothing left to end, still a clean redirect.
        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken));

        AssertRedirectedToRp(response);
    }

    [Fact]
    public async Task WithoutPostLogoutRedirectUri_ShowsSignedOutPage()
    {
        var alice = await SignInAsync(_host);

        var response = await alice.Browser.GetAsync(EndSessionUrl(alice.IdToken, postLogoutRedirectUri: null, state: null));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/tenants/acme/signed-out", response.Headers.Location!.OriginalString);
        await AssertEndedAsync(_host, alice.Sid);
        var page = await alice.Browser.GetAsync("/tenants/acme/signed-out");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("You have been signed out", await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownTenant_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _host.CreateBrowser().GetAsync("/tenants/initech/endsession")).StatusCode);

    [Theory]
    [InlineData("/tenants/initech/logout")]
    [InlineData("/tenants/frozen/logout")]
    [InlineData("/tenants/initech/signed-out")]
    public async Task Pages_UnknownOrSuspendedTenant_Return404(string path) =>
        Assert.Equal(HttpStatusCode.NotFound, (await _host.CreateBrowser().GetAsync(path)).StatusCode);

    private async Task<string> GlobexIdTokenAsync()
    {
        _host.Directory.Users.Add(new FakeDirectory.UserEntry(_host.Directory.Tenant("globex").TenantId, "gina", UserStatus.Active));
        var pkce = OidcFlow.NewPkce();
        var authorize = await OidcFlow.AuthorizeWithLoginAsync(_host.CreateBrowser(), "globex",
            OidcFlow.AuthorizeParameters("web-rt", FakeDirectory.WebRedirect, pkce, "openid"), username: "gina");
        var code = OidcFlow.RedirectQuery(authorize, FakeDirectory.WebRedirect)["code"];
        var response = await _host.Client.PostAsync("/tenants/globex/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "web-rt",
            ["client_secret"] = FakeDirectory.GlobexSecret,
            ["code"] = code,
            ["redirect_uri"] = FakeDirectory.WebRedirect,
            ["code_verifier"] = pkce.Verifier
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await TokenFlow.JsonAsync(response)).GetProperty("id_token").GetString()!;
    }
}
