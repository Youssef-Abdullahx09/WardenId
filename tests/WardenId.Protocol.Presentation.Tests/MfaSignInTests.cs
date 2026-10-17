using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using Command = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>The MFA steps of the hosted login: code step, enrollment, the pending ticket in between.</summary>
public partial class MfaSignInTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    private readonly FakeTimeProvider _clock = new(Start);
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync(_clock);

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private const string ReturnUrl = "/tenants/acme/authorize?client_id=web&state=xyz";
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

    private static async Task<HttpResponseMessage> PostCodeAsync(BrowserClient browser, string path, string code)
    {
        var page = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var token = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        return await browser.PostFormAsync(path, new Dictionary<string, string>
        {
            ["Code"] = code,
            ["__RequestVerificationToken"] = token
        });
    }

    private static bool HasCookie(BrowserClient browser, string name) =>
        browser.Cookies.GetAllCookies().Any(c => c.Name == name && !c.Expired);

    private IReadOnlyList<Command> Events(SecurityEvent securityEvent) =>
        _host.AuditLog.Events.Where(e => e.Event == securityEvent).ToList();

    [Fact]
    public async Task Password_WithAFactor_GoesToTheCodeStep_WithAPendingTicket_AndNoSession()
    {
        var browser = _host.CreateBrowser();

        var response = await PostLoginAsync(browser, "erin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(CodePath, response.Headers.Location!.OriginalString);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(PendingSignIn.CookieName + "="));
        Assert.Contains("path=/tenants/acme", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.False(HasCookie(browser, TenantSession.Scheme));
        Assert.Empty(await _host.SessionsOfAsync(_host.Directory.User("erin").UserId));
        Assert.Empty(Events(SecurityEvent.SessionStarted));
        Assert.Empty(Events(SecurityEvent.SignInFailed));
    }

    [Fact]
    public async Task CodeStep_RendersTheForm_RecoveryLink_AndCancel()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "erin");

        var response = await browser.GetAsync(CodePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"Code\"", html);
        Assert.Contains("autocomplete=\"one-time-code\"", html);
        Assert.Contains("Use a recovery code", html);
        Assert.Contains("name=\"cancel\"", html);
        Assert.DoesNotContain(_host.Directory.User("erin").UserId.ToString(), html);
    }

    [Fact]
    public async Task CodeStep_RightCode_StartsTheSession_AndReturnsToAuthorize()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "erin");

        var response = await PostCodeAsync(browser, CodePath, FakeDirectory.TotpCode);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ReturnUrl, response.Headers.Location!.OriginalString);
        Assert.True(HasCookie(browser, TenantSession.Scheme));
        Assert.False(HasCookie(browser, PendingSignIn.CookieName));
        Assert.Single(await _host.SessionsOfAsync(_host.Directory.User("erin").UserId));
        Assert.Equal(["pwd", "otp", "mfa"], Assert.Single(Events(SecurityEvent.SessionStarted)).Session!.Amr);
    }

    [Fact]
    public async Task CodeStep_RecoveryCode_Works()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "erin");

        var page = await browser.GetAsync(CodePath + "?UseRecoveryCode=true");
        Assert.Contains("Recovery code", await page.Content.ReadAsStringAsync());
        var response = await PostCodeAsync(browser, CodePath + "?UseRecoveryCode=true", "erin1-recov");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(HasCookie(browser, TenantSession.Scheme));
        Assert.Equal(["pwd", "mfa"], Assert.Single(Events(SecurityEvent.SessionStarted)).Session!.Amr);
    }

    [Fact]
    public async Task CodeStep_WrongCode_ShowsOneGenericMessage_AuditsIt_AndStartsNoSession()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "erin");

        var response = await PostCodeAsync(browser, CodePath, "000000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("That code didn&#x27;t work. Please try again.", await response.Content.ReadAsStringAsync());
        Assert.False(HasCookie(browser, TenantSession.Scheme));
        var failed = Assert.Single(Events(SecurityEvent.SignInFailed));
        Assert.Equal(OutcomeReason.InvalidSecondFactor, failed.Reason);
        Assert.Equal(Actor.User(_host.Directory.User("erin").UserId), failed.Actor);

        // The ticket survives a wrong code: the next try can still succeed.
        var retry = await PostCodeAsync(browser, CodePath, FakeDirectory.TotpCode);
        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
    }

    [Fact]
    public async Task StepPages_WithoutATicket_SendTheUserToLogin()
    {
        var browser = _host.CreateBrowser();

        var code = await browser.GetAsync(CodePath);
        var enroll = await browser.GetAsync(EnrollPath);

        Assert.Equal("/tenants/acme/login", code.Headers.Location!.OriginalString);
        Assert.Equal("/tenants/acme/login", enroll.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/tenants/initech/login/code")]
    [InlineData("/tenants/frozen/login/enroll-totp")]
    public async Task StepPages_UnknownOrSuspendedTenant_Return404(string path) =>
        Assert.Equal(HttpStatusCode.NotFound, (await _host.CreateBrowser().GetAsync(path)).StatusCode);

    [Fact]
    public async Task Ticket_Expires_AfterFiveMinutes()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "erin");

        _clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var response = await browser.GetAsync(CodePath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/tenants/acme/login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Ticket_IsUselessForAnotherTenant()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "erin");
        var ticket = browser.Cookies.GetAllCookies().Single(c => c.Name == PendingSignIn.CookieName);

        // The browser would never send it there (Path); a hand-made request does.
        var request = new HttpRequestMessage(HttpMethod.Get, "/tenants/globex/login/code");
        request.Headers.Add("Cookie", $"{ticket.Name}={ticket.Value}");
        var response = await _host.CreateBrowser().SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/tenants/globex/login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task EnrollMfa_GoesToEnrollment_ShowsQrAndSecret_WithoutPostingTheSecret()
    {
        var browser = _host.CreateBrowser();

        var login = await PostLoginAsync(browser, "frank");
        Assert.Equal(EnrollPath, login.Headers.Location!.OriginalString);
        var response = await browser.GetAsync(EnrollPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<svg", html);
        Assert.Contains("JBSW Y3DP EHPK 3PXP", html);
        Assert.DoesNotContain(FakeDirectory.TotpSecret, FormFieldValues(html));
        Assert.DoesNotContain("otpauth://", html);
        Assert.Contains("name=\"cancel\"", html);
        Assert.False(HasCookie(browser, TenantSession.Scheme));
    }

    [Fact]
    public async Task Enrollment_IsStartedOnce_PerTicket()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "frank");

        await browser.GetAsync(EnrollPath);
        await browser.GetAsync(EnrollPath);

        Assert.Equal(1, _host.Directory.User("frank").BeginEnrollmentCalls);
    }

    [Fact]
    public async Task Enrollment_NamesTheTenantByItsDisplayName_NotItsSlug()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "frank");

        await browser.GetAsync(EnrollPath);

        Assert.Equal(FakeDirectory.AcmeDisplayName, _host.Directory.User("frank").LastEnrollmentIssuer);
    }

    [Fact]
    public async Task Enrollment_WrongCode_ReRendersTheSameQr_AndAuditsIt()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "frank");

        var response = await PostCodeAsync(browser, EnrollPath, "000000");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("That code didn&#x27;t work. Please try again.", html);
        Assert.Contains("<svg", html);
        Assert.Contains("JBSW Y3DP EHPK 3PXP", html);
        Assert.Equal(1, _host.Directory.User("frank").BeginEnrollmentCalls);
        Assert.Equal(OutcomeReason.InvalidSecondFactor, Assert.Single(Events(SecurityEvent.SignInFailed)).Reason);
        Assert.False(HasCookie(browser, TenantSession.Scheme));
    }

    [Fact]
    public async Task Enrollment_RightCode_ShowsRecoveryCodesOnce_ThenContinue()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "frank");

        var response = await PostCodeAsync(browser, EnrollPath, FakeDirectory.TotpCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Save your recovery codes", html);
        Assert.Contains("code01-frank", html);
        Assert.Contains("code10-frank", html);
        Assert.Contains($"href=\"{WebUtility.HtmlEncode(ReturnUrl)}\"", html);
        Assert.True(HasCookie(browser, TenantSession.Scheme));
        Assert.False(HasCookie(browser, PendingSignIn.CookieName));
        Assert.DoesNotContain(RequiredAction.EnrollMfa, _host.Directory.User("frank").PendingActions);
        Assert.Equal(["pwd", "otp", "mfa"], Assert.Single(Events(SecurityEvent.SessionStarted)).Session!.Amr);
        var frank = _host.Directory.User("frank");
        var enrolled = Assert.Single(Events(SecurityEvent.TotpEnrolled));
        Assert.Equal(Actor.User(frank.UserId), enrolled.Actor);
        Assert.Equal([Target.User(frank.UserId)], enrolled.Targets);
        Assert.Empty(Events(SecurityEvent.AuthenticatorReplaced));
        Assert.All(
            _host.ActorScope.Observed.Where(o => o.Request is "BeginTotpEnrollment" or "CompleteTotpEnrollment"),
            o => Assert.Equal((frank.TenantId, frank.UserId), o.ActingAs));

        // Shown once: the ticket is gone, so the page sends the user back to login.
        Assert.Equal("/tenants/acme/login", (await browser.GetAsync(EnrollPath)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Enrollment_WithoutReturnUrl_SaysSignedIn()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "frank", returnUrl: null);

        var html = await (await PostCodeAsync(browser, EnrollPath, FakeDirectory.TotpCode)).Content.ReadAsStringAsync();

        Assert.Contains("code01-frank", html);
        Assert.Contains("You are signed in", html);
        Assert.DoesNotContain(">Continue<", html);
    }

    [Fact]
    public async Task FactorAndEnrollMfa_CodeStepFirst_ThenEnrollment()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "gina");

        // Enrollment isn't reachable before the code step.
        Assert.Equal(CodePath, (await browser.GetAsync(EnrollPath)).Headers.Location!.OriginalString);

        var afterCode = await PostCodeAsync(browser, CodePath, FakeDirectory.TotpCode);
        Assert.Equal(EnrollPath, afterCode.Headers.Location!.OriginalString);
        Assert.False(HasCookie(browser, TenantSession.Scheme));

        var done = await PostCodeAsync(browser, EnrollPath, FakeDirectory.TotpCode);
        Assert.Contains("code01-gina", await done.Content.ReadAsStringAsync());
        Assert.True(HasCookie(browser, TenantSession.Scheme));

        // The admin-required new authenticator replaced gina's old one.
        Assert.Single(Events(SecurityEvent.AuthenticatorReplaced));
        Assert.Empty(Events(SecurityEvent.TotpEnrolled));
        var gina = _host.Directory.User("gina");
        var (_, actingAs) = Assert.Single(_host.ActorScope.Observed, o => o.Request == "VerifySecondFactor");
        Assert.Equal((gina.TenantId, gina.UserId), actingAs);
    }

    [Fact]
    public async Task UserWithoutMfa_StillSignsInDirectly_AndAStaleTicketIsDropped()
    {
        var browser = _host.CreateBrowser();
        await PostLoginAsync(browser, "erin");

        var response = await PostLoginAsync(browser, "alice");

        Assert.Equal(ReturnUrl, response.Headers.Location!.OriginalString);
        Assert.True(HasCookie(browser, TenantSession.Scheme));
        Assert.False(HasCookie(browser, PendingSignIn.CookieName));
        Assert.Equal(["pwd"], Assert.Single(Events(SecurityEvent.SessionStarted)).Session!.Amr);
    }

    [Fact]
    public async Task FullFlow_CodeStepThenAuthorize_IssuesACode()
    {
        var browser = _host.CreateBrowser();
        var parameters = OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, OidcFlow.NewPkce(), "openid");
        var authorize = await browser.GetAsync(OidcFlow.AuthorizeUrl("acme", parameters));
        var loginUrl = authorize.Headers.Location!.OriginalString;
        var returnUrl = QueryHelpers.ParseQuery(new Uri("https://x" + loginUrl).Query)["ReturnUrl"].ToString();

        await PostLoginAsync(browser, "erin", returnUrl);
        var afterCode = await PostCodeAsync(browser, CodePath, FakeDirectory.TotpCode);
        var callback = await browser.GetAsync(afterCode.Headers.Location!.OriginalString);

        Assert.True(OidcFlow.RedirectQuery(callback, FakeDirectory.WebRedirect).ContainsKey("code"));
    }

    private static string FormFieldValues(string html) =>
        string.Join('\n', InputValuePattern().Matches(html).Select(m => m.Groups[1].Value));

    [GeneratedRegex("<input[^>]*value=\"([^\"]*)\"")]
    private static partial Regex InputValuePattern();
}
