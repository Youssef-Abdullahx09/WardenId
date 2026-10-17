using System.Net;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>The terms step of the hosted login (AcceptTos): a step after the code step, before enrollment.</summary>
public class LoginTermsTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private const string ReturnUrl = "/tenants/acme/authorize?client_id=web&state=xyz";
    private const string TermsPath = "/tenants/acme/login/terms";
    private const string CodePath = "/tenants/acme/login/code";
    private const string EnrollPath = "/tenants/acme/login/enroll-totp";

    private static async Task<HttpResponseMessage> PostLoginAsync(BrowserClient browser, string user, string? returnUrl = ReturnUrl)
    {
        var loginUrl = "/tenants/acme/login" + (returnUrl is null ? "" : $"?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        var page = await browser.GetAsync(loginUrl);
        var token = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        return await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = user,
            ["Password"] = FakeDirectory.UserPassword,
            ["ReturnUrl"] = returnUrl ?? "",
            ["__RequestVerificationToken"] = token
        });
    }

    private static async Task<HttpResponseMessage> PostFormAsync(BrowserClient browser, string path, Dictionary<string, string> form)
    {
        var page = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        form["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        return await browser.PostFormAsync(path, form);
    }

    private static Task<HttpResponseMessage> PostAcceptAsync(BrowserClient browser, bool accept = true) =>
        PostFormAsync(browser, TermsPath, accept ? new Dictionary<string, string> { ["Accept"] = "true" } : []);

    private static bool HasCookie(BrowserClient browser, string name) =>
        browser.Cookies.GetAllCookies().Any(c => c.Name == name && !c.Expired);

    [Fact]
    public async Task AcceptTos_IsAStep_NotABlock_AndIsNotAudited()
    {
        var browser = _host.CreateBrowser();

        var response = await PostLoginAsync(browser, "tess");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(TermsPath, response.Headers.Location!.OriginalString);
        Assert.True(HasCookie(browser, PendingSignIn.CookieName));
        Assert.False(HasCookie(browser, TenantSession.Scheme));
        Assert.Empty(_host.AuditLog.Events);
    }

    [Fact]
    public async Task TermsPage_LinksTheTerms_InANewTab_WithSecurityHeaders()
    {
        _host.Directory.TermsUrl = "https://acme.example/terms?v=2&lang=en";
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "tess");

        var response = await browser.GetAsync(TermsPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "<a href=\"https://acme.example/terms?v=2&amp;lang=en\" target=\"_blank\" rel=\"noopener noreferrer\">", html);
        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("required", html);
        Assert.Contains("name=\"cancel\"", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("javascript:alert(1)")]
    public async Task TermsPage_WithoutAWebLink_ShowsNeutralText(string? termsUrl)
    {
        _host.Directory.TermsUrl = termsUrl;
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "tess");

        var html = await (await browser.GetAsync(TermsPath)).Content.ReadAsStringAsync();

        Assert.Contains("please accept the terms of service", html);
        Assert.DoesNotContain("target=\"_blank\"", html);
        Assert.DoesNotContain("javascript:", html);
    }

    [Fact]
    public async Task Continue_WithoutTicking_RefusesAndStartsNoSession()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "tess");

        var response = await PostAcceptAsync(browser, accept: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Please accept the terms of service to continue.", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, _host.Directory.User("tess").AcceptTermsCalls);
        Assert.False(HasCookie(browser, TenantSession.Scheme));
    }

    [Fact]
    public async Task Accepting_ClearsTheAction_StartsTheSession_AndReturnsToAuthorize()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "tess");

        var response = await PostAcceptAsync(browser);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ReturnUrl, response.Headers.Location!.OriginalString);
        var tess = _host.Directory.User("tess");
        Assert.DoesNotContain(RequiredAction.AcceptTos, tess.PendingActions);
        Assert.NotNull(tess.TermsAcceptedAt);
        Assert.True(HasCookie(browser, TenantSession.Scheme));
        Assert.False(HasCookie(browser, PendingSignIn.CookieName));
        var started = Assert.Single(_host.AuditLog.Events);
        Assert.Equal(SecurityEvent.SessionStarted, started.Event);
        Assert.Equal(["pwd"], started.Session!.Amr);
    }

    [Fact]
    public async Task Accepting_WithoutReturnUrl_SaysSignedIn_AndLinksTheAccount()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "tess", returnUrl: null);

        var response = await PostAcceptAsync(browser);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("You are signed in", html);
        Assert.Contains("href=\"/tenants/acme/account\"", html);
    }

    [Fact]
    public async Task StepOrder_Code_ThenTerms_ThenEnrollment_KeepingTheAmr()
    {
        var browser = _host.CreateBrowser();
        Assert.Equal(CodePath, (await PostLoginAsync(browser, "hank")).Headers.Location!.OriginalString);

        // Neither later step is reachable early.
        Assert.Equal(CodePath, (await browser.GetAsync(TermsPath)).Headers.Location!.OriginalString);
        Assert.Equal(CodePath, (await browser.GetAsync(EnrollPath)).Headers.Location!.OriginalString);

        var afterCode = await PostFormAsync(browser, CodePath, new() { ["Code"] = FakeDirectory.TotpCode });
        Assert.Equal(TermsPath, afterCode.Headers.Location!.OriginalString);
        Assert.Equal(TermsPath, (await browser.GetAsync(EnrollPath)).Headers.Location!.OriginalString);

        var afterTerms = await PostAcceptAsync(browser);
        Assert.Equal(EnrollPath, afterTerms.Headers.Location!.OriginalString);
        Assert.False(HasCookie(browser, TenantSession.Scheme));

        var done = await PostFormAsync(browser, EnrollPath, new() { ["Code"] = FakeDirectory.TotpCode });
        Assert.Contains("code01-hank", await done.Content.ReadAsStringAsync());
        Assert.True(HasCookie(browser, TenantSession.Scheme));
        Assert.Equal(["pwd", "otp", "mfa"], Assert.Single(_host.AuditLog.Events).Session!.Amr);
    }

    [Fact]
    public async Task TermsPage_WithoutATicket_SendsTheUserToLogin()
    {
        var response = await _host.CreateBrowser().GetAsync(TermsPath);

        Assert.Equal("/tenants/acme/login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Post_WithoutAntiforgery_IsRejected()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "tess");

        var response = await browser.PostFormAsync(TermsPath, new Dictionary<string, string> { ["Accept"] = "true" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _host.Directory.User("tess").AcceptTermsCalls);
    }
}
