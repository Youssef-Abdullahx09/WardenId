using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Domain.Sessions;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>The self-service account pages under /tenants/{slug}/account.</summary>
public partial class AccountPagesTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    private readonly FakeTimeProvider _clock = new(Start);
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync(_clock);

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private const string AccountPath = "/tenants/acme/account";
    private const string PasswordPath = AccountPath + "/password";
    private const string SetupPath = AccountPath + "/authenticator";
    private const string ReplacePath = AccountPath + "/authenticator/replace";
    private const string RecoveryCodesPath = AccountPath + "/recovery-codes";
    private const string RemovePath = AccountPath + "/two-step/remove";
    private const string WrongCode = "That code didn&#x27;t work. Please try again.";

    private static string LoginFor(string returnUrl) => $"/tenants/acme/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";

    /// <summary>A browser with a live session for <paramref name="user"/> (code step done when the user has a factor).</summary>
    private async Task<BrowserClient> SignedInAsync(string user, string slug = "acme")
    {
        var browser = _host.CreateBrowser();
        var loginUrl = $"/tenants/{slug}/login";
        var page = await browser.GetAsync(loginUrl);
        var login = await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = user,
            ["Password"] = FakeDirectory.UserPassword,
            ["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync())
        });
        if (login.StatusCode == HttpStatusCode.Redirect)
            await PostAsync(browser, login.Headers.Location!.OriginalString, new() { ["Code"] = FakeDirectory.TotpCode });

        Assert.True(HasSession(browser));
        return browser;
    }

    /// <summary>Loads <paramref name="path"/> for its antiforgery token, then posts the form there (or to <paramref name="handler"/>).</summary>
    private static async Task<HttpResponseMessage> PostAsync(
        BrowserClient browser, string path, Dictionary<string, string> form, string? handler = null)
    {
        var page = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        form["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        return await browser.PostFormAsync(handler is null ? path : $"{path}?handler={handler}", form);
    }

    /// <summary>Posts to a page using the antiforgery token and hidden state already in <paramref name="html"/>.</summary>
    private static Task<HttpResponseMessage> PostFromAsync(
        BrowserClient browser, string html, string path, Dictionary<string, string> form, string? handler = null)
    {
        form["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(html);
        return browser.PostFormAsync(handler is null ? path : $"{path}?handler={handler}", form);
    }

    private static bool HasSession(BrowserClient browser) =>
        browser.Cookies.GetAllCookies().Any(c => c.Name == TenantSession.Scheme && !c.Expired);

    private static string State(string html)
    {
        var match = StatePattern().Match(html);
        Assert.True(match.Success, "No enrollment state in page.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<string> HtmlAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    // --- Session and return URL ---

    [Theory]
    [InlineData(AccountPath)]
    [InlineData(PasswordPath)]
    [InlineData(ReplacePath)]
    [InlineData(RemovePath)]
    public async Task WithoutASession_RedirectsToLogin_ComingBackToThePage(string path)
    {
        var response = await _host.CreateBrowser().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(LoginFor(path), response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task SigningIn_FromTheRedirect_LandsOnTheAccountPage()
    {
        var browser = _host.CreateBrowser();
        var login = (await browser.GetAsync(AccountPath)).Headers.Location!.OriginalString;
        var page = await browser.GetAsync(login);

        var response = await browser.PostFormAsync(login, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = "alice",
            ["Password"] = FakeDirectory.UserPassword,
            ["ReturnUrl"] = AccountPath,
            ["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync())
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(AccountPath, response.Headers.Location!.OriginalString);
        Assert.Contains("alice@example.test", await HtmlAsync(await browser.GetAsync(AccountPath)));
    }

    [Fact]
    public async Task Login_AccountReturnUrl_HasNoCancelButton()
    {
        var html = await HtmlAsync(await _host.CreateBrowser().GetAsync(LoginFor(AccountPath)));

        Assert.DoesNotContain("name=\"cancel\"", html);
        Assert.Contains($"value=\"{AccountPath}\"", html);
    }

    [Theory]
    [InlineData("/tenants/globex/account")]
    [InlineData("/tenants/acme/token")]
    [InlineData("https://evil.example/tenants/acme/account")]
    [InlineData("//evil.example/tenants/acme/account")]
    [InlineData("/tenants/acme/account/../token")]
    public async Task Login_RefusesOtherReturnUrls_AndStaysOnTheLoginPage(string returnUrl)
    {
        var browser = _host.CreateBrowser();
        var loginUrl = LoginFor(returnUrl);
        var page = await browser.GetAsync(loginUrl);

        var response = await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = "alice",
            ["Password"] = FakeDirectory.UserPassword,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync())
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You are signed in", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnotherTenantsSession_DoesNotOpenTheAccount()
    {
        var browser = await SignedInAsync("gwen", "globex");
        var cookie = browser.Cookies.GetAllCookies().Single(c => c.Name == TenantSession.Scheme);

        // The browser wouldn't send it here (Path); a hand-made request does.
        var request = new HttpRequestMessage(HttpMethod.Get, AccountPath);
        request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
        var response = await _host.CreateBrowser().SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(LoginFor(AccountPath), response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/tenants/initech/account")]
    [InlineData("/tenants/frozen/account/password")]
    public async Task UnknownOrSuspendedTenant_Returns404(string path) =>
        Assert.Equal(HttpStatusCode.NotFound, (await _host.CreateBrowser().GetAsync(path)).StatusCode);

    [Fact]
    public async Task LoginPage_LinksTheAccount_WhenAlreadySignedIn()
    {
        var anonymous = await HtmlAsync(await _host.CreateBrowser().GetAsync("/tenants/acme/login"));
        Assert.DoesNotContain("Manage your account", anonymous);

        var browser = await SignedInAsync("alice");
        var html = await HtmlAsync(await browser.GetAsync("/tenants/acme/login"));

        Assert.Contains("Manage your account", html);
        Assert.Contains("href=\"/tenants/acme/account\"", html);
    }

    // --- Overview ---

    [Fact]
    public async Task Overview_WithoutMfa_ShowsTheUser_AndOffersSetup()
    {
        var browser = await SignedInAsync("alice");

        var response = await browser.GetAsync(AccountPath);

        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var html = await HtmlAsync(response);
        Assert.Contains("alice@example.test", html);
        Assert.Contains("<dd>alice</dd>", html);
        Assert.Contains($"href=\"{SetupPath}\"", html);
        Assert.Contains($"href=\"{PasswordPath}\"", html);
        Assert.DoesNotContain(ReplacePath, html);
        Assert.Contains("action=\"/tenants/acme/endsession\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.DoesNotContain(_host.Directory.User("alice").UserId.ToString(), html);
    }

    [Fact]
    public async Task Overview_MfaOff_OffersNoSetup()
    {
        _host.Directory.MfaAvailable = false;
        var browser = await SignedInAsync("alice");

        var html = await HtmlAsync(await browser.GetAsync(AccountPath));

        Assert.DoesNotContain(SetupPath + "\"", html);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync(SetupPath)).StatusCode);
    }

    [Fact]
    public async Task Overview_WithMfa_ShowsTheFactor_WarnsAboutFewRecoveryCodes_AndOffersTheActions()
    {
        var browser = await SignedInAsync("erin");

        var html = await HtmlAsync(await browser.GetAsync(AccountPath));

        Assert.Contains("Set up 2026-09-01 10:00 UTC · last used never", html);
        Assert.Contains("class=\"notice\"", html);
        Assert.Contains("Only 1 recovery codes left", html);
        Assert.Contains($"href=\"{ReplacePath}\"", html);
        Assert.Contains($"href=\"{RecoveryCodesPath}\"", html);
        Assert.Contains($"href=\"{RemovePath}\"", html);
        Assert.DoesNotContain($"href=\"{SetupPath}\"", html);
    }

    [Fact]
    public async Task Overview_MfaRequired_HidesRemove()
    {
        _host.Directory.MfaRequired = true;
        var browser = await SignedInAsync("erin");

        var html = await HtmlAsync(await browser.GetAsync(AccountPath));

        Assert.DoesNotContain(RemovePath, html);
        Assert.Equal(AccountPath, (await browser.GetAsync(RemovePath)).Headers.Location!.OriginalString);
    }

    // --- Change password ---

    [Fact]
    public async Task ChangePassword_Mismatch_IsRefusedLocally()
    {
        var browser = await SignedInAsync("alice");

        var response = await PostAsync(browser, PasswordPath, new()
        {
            ["CurrentPassword"] = FakeDirectory.UserPassword,
            ["NewPassword"] = "a brand new password",
            ["ConfirmPassword"] = "a different password"
        });

        Assert.Contains("The new passwords don&#x27;t match.", await HtmlAsync(response));
        Assert.False(_host.Directory.User("alice").PasswordChanged);
        Assert.True(HasSession(browser));
    }

    [Theory]
    [InlineData("wrong", "a brand new password", ChangeOwnPasswordHandler.WrongPassword)]
    [InlineData(FakeDirectory.UserPassword, "short", ChangeOwnPasswordHandler.TooShort)]
    public async Task ChangePassword_IdentityRefusal_IsShown(string current, string next, string message)
    {
        var browser = await SignedInAsync("alice");

        var response = await PostAsync(browser, PasswordPath, new()
        {
            ["CurrentPassword"] = current, ["NewPassword"] = next, ["ConfirmPassword"] = next
        });

        var html = await HtmlAsync(response);
        Assert.Contains(WebUtility.HtmlEncode(message), html);
        Assert.DoesNotContain(current, FormFieldValues(html));
        Assert.True(HasSession(browser));
        Assert.Empty(Events(SecurityEvent.PasswordChanged));
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_IsAuditedAsAFailedStepUp()
    {
        var browser = await SignedInAsync("alice");

        await PostAsync(browser, PasswordPath, new()
        {
            ["CurrentPassword"] = "wrong", ["NewPassword"] = "a brand new password", ["ConfirmPassword"] = "a brand new password"
        });

        var alice = _host.Directory.User("alice");
        var sid = Assert.Single(await _host.SessionsOfAsync(alice.UserId)).Id;
        AssertStepUpFailed(Assert.Single(Events(SecurityEvent.StepUpFailed)), alice, sid, OutcomeReason.InvalidPassword);
    }

    [Fact]
    public async Task ChangePassword_PolicyRefusal_IsNotAStepUpFailure()
    {
        var browser = await SignedInAsync("alice");

        await PostAsync(browser, PasswordPath, new()
        {
            ["CurrentPassword"] = FakeDirectory.UserPassword, ["NewPassword"] = "short", ["ConfirmPassword"] = "short"
        });

        Assert.Empty(Events(SecurityEvent.StepUpFailed));
    }

    [Fact]
    public async Task ChangePassword_Success_ClearsTheCookie_AndAsksToSignInAgain()
    {
        var browser = await SignedInAsync("alice");

        var response = await PostAsync(browser, PasswordPath, new()
        {
            ["CurrentPassword"] = FakeDirectory.UserPassword,
            ["NewPassword"] = "a brand new password",
            ["ConfirmPassword"] = "a brand new password"
        });

        var html = await HtmlAsync(response);
        Assert.Contains("Password changed", html);
        Assert.Contains($"href=\"{WebUtility.HtmlEncode(LoginFor(AccountPath))}\"", html);
        Assert.True(_host.Directory.User("alice").PasswordChanged);
        Assert.False(HasSession(browser));
        var alice = _host.Directory.User("alice");
        var sid = Assert.Single(await _host.SessionsOfAsync(alice.UserId)).Id;
        AssertUserChange(Assert.Single(Events(SecurityEvent.PasswordChanged)), alice, sid);
        var (_, actingAs) = Assert.Single(_host.ActorScope.Observed, o => o.Request == "ChangeOwnPassword");
        Assert.Equal((alice.TenantId, alice.UserId), actingAs);
    }

    // --- Set up an authenticator app ---

    [Fact]
    public async Task Setup_Get_StartsNothing()
    {
        var browser = await SignedInAsync("alice");

        var html = await HtmlAsync(await browser.GetAsync(SetupPath));

        Assert.Contains("handler=Start", html);
        Assert.Equal(0, _host.Directory.User("alice").BeginEnrollmentCalls);
    }

    [Fact]
    public async Task Setup_NamesTheTenantByItsDisplayName_NotItsSlug()
    {
        var browser = await SignedInAsync("alice");

        await HtmlAsync(await PostAsync(browser, SetupPath, [], handler: "Start"));

        Assert.Equal(FakeDirectory.AcmeDisplayName, _host.Directory.User("alice").LastEnrollmentIssuer);
    }

    [Fact]
    public async Task Setup_Flow_QrThenWrongCodeThenRightCode_ShowsRecoveryCodesOnce()
    {
        var browser = await SignedInAsync("alice");

        var started = await HtmlAsync(await PostAsync(browser, SetupPath, [], handler: "Start"));
        Assert.Contains("<div class=\"qr\"><svg", started);
        Assert.Contains("JBSW Y3DP EHPK 3PXP", started);
        Assert.DoesNotContain(FakeDirectory.TotpSecret, FormFieldValues(started));
        Assert.DoesNotContain("otpauth://", started);
        var state = State(started);

        var wrong = await HtmlAsync(await PostFromAsync(browser, started, SetupPath,
            new() { ["State"] = state, ["Code"] = "000000" }, handler: "Verify"));
        Assert.Contains(WrongCode, wrong);
        Assert.Contains("JBSW Y3DP EHPK 3PXP", wrong);
        Assert.Equal(state, State(wrong));

        var done = await HtmlAsync(await PostFromAsync(browser, wrong, SetupPath,
            new() { ["State"] = state, ["Code"] = FakeDirectory.TotpCode }, handler: "Verify"));
        Assert.Contains("code01-alice", done);
        Assert.DoesNotContain("name=\"State\"", done);
        Assert.True(_host.Directory.User("alice").HasFactor);
        Assert.Equal(1, _host.Directory.User("alice").BeginEnrollmentCalls);

        // A wrong code here is not a failed sign-in but a failed step-up; a first setup ends no session.
        Assert.DoesNotContain(_host.AuditLog.Events, e => e.Event == SecurityEvent.SignInFailed);
        var alice = _host.Directory.User("alice");
        var sid = Assert.Single(await _host.SessionsOfAsync(alice.UserId)).Id;
        AssertStepUpFailed(Assert.Single(Events(SecurityEvent.StepUpFailed)), alice, sid, OutcomeReason.InvalidSecondFactor);
        AssertUserChange(Assert.Single(Events(SecurityEvent.TotpEnrolled)), alice, sid);
        Assert.Empty(Events(SecurityEvent.AuthenticatorReplaced));
        Assert.Null((await _host.FindSessionAsync(sid))!.EndedAt);
    }

    [Fact]
    public async Task Setup_AlreadyHasMfa_RedirectsToTheOverview()
    {
        var browser = await SignedInAsync("erin");

        Assert.Equal(AccountPath, (await browser.GetAsync(SetupPath)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Setup_StateOfAnotherUser_IsRefused()
    {
        var dave = await SignedInAsync("dave");
        var daveState = State(await HtmlAsync(await PostAsync(dave, SetupPath, [], handler: "Start")));

        var alice = await SignedInAsync("alice");
        var page = await HtmlAsync(await alice.GetAsync(SetupPath));
        var response = await PostFromAsync(alice, page, SetupPath,
            new() { ["State"] = daveState, ["Code"] = FakeDirectory.TotpCode }, handler: "Verify");

        Assert.Contains("took too long", await HtmlAsync(response));
        Assert.Equal(0, _host.Directory.User("alice").CompleteEnrollmentCalls);
        Assert.Equal(0, _host.Directory.User("dave").CompleteEnrollmentCalls);
        Assert.False(_host.Directory.User("dave").HasFactor);
    }

    [Fact]
    public async Task Setup_StateFromTheReplacePage_IsRefused()
    {
        var browser = await SignedInAsync("erin");
        var replaceState = State(await HtmlAsync(await PostAsync(browser, ReplacePath,
            new() { ["CurrentCode"] = FakeDirectory.TotpCode }, handler: "Start")));
        _host.Directory.User("erin").HasFactor = false;

        var page = await HtmlAsync(await browser.GetAsync(SetupPath));
        var response = await PostFromAsync(browser, page, SetupPath,
            new() { ["State"] = replaceState, ["Code"] = FakeDirectory.TotpCode }, handler: "Verify");

        Assert.Contains("took too long", await HtmlAsync(response));
        Assert.Equal(0, _host.Directory.User("erin").CompleteEnrollmentCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-protected-value")]
    public async Task Setup_MissingOrTamperedState_IsRefused(string state)
    {
        var browser = await SignedInAsync("alice");
        var page = await HtmlAsync(await browser.GetAsync(SetupPath));

        var response = await PostFromAsync(browser, page, SetupPath,
            new() { ["State"] = state, ["Code"] = FakeDirectory.TotpCode }, handler: "Verify");

        Assert.Contains("took too long", await HtmlAsync(response));
        Assert.Equal(0, _host.Directory.User("alice").CompleteEnrollmentCalls);
    }

    [Fact]
    public async Task Setup_State_ExpiresAfterTenMinutes()
    {
        var browser = await SignedInAsync("alice");
        var started = await HtmlAsync(await PostAsync(browser, SetupPath, [], handler: "Start"));

        _clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        var response = await PostFromAsync(browser, started, SetupPath,
            new() { ["State"] = State(started), ["Code"] = FakeDirectory.TotpCode }, handler: "Verify");

        Assert.Contains("took too long", await HtmlAsync(response));
        Assert.False(_host.Directory.User("alice").HasFactor);
    }

    // --- Replace the authenticator app ---

    [Fact]
    public async Task Replace_WrongCurrentCode_IsAnsweredGenerically_AndStartsNothing()
    {
        var browser = await SignedInAsync("erin");

        var response = await PostAsync(browser, ReplacePath, new() { ["CurrentCode"] = "000000" }, handler: "Start");

        var html = await HtmlAsync(response);
        Assert.Contains(WrongCode, html);
        Assert.DoesNotContain("<div class=\"qr\"><svg", html);
        Assert.Equal(0, _host.Directory.User("erin").BeginEnrollmentCalls);
        Assert.DoesNotContain(_host.AuditLog.Events, e => e.Event == SecurityEvent.SignInFailed);
        var erin = _host.Directory.User("erin");
        var sid = Assert.Single(await _host.SessionsOfAsync(erin.UserId)).Id;
        AssertStepUpFailed(Assert.Single(Events(SecurityEvent.StepUpFailed)), erin, sid, OutcomeReason.InvalidSecondFactor);
    }

    [Fact]
    public async Task Replace_WithARecoveryCode_ThenTheNewAppsCode_ShowsNewRecoveryCodes()
    {
        var browser = await SignedInAsync("erin");

        var started = await HtmlAsync(await PostAsync(browser, ReplacePath,
            new() { ["CurrentCode"] = "erin1-recov" }, handler: "Start"));
        Assert.Contains("JBSW Y3DP EHPK 3PXP", started);
        Assert.Equal(FakeDirectory.AcmeDisplayName, _host.Directory.User("erin").LastEnrollmentIssuer);

        var done = await HtmlAsync(await PostFromAsync(browser, started, ReplacePath,
            new() { ["State"] = State(started), ["Code"] = FakeDirectory.TotpCode }, handler: "Verify"));

        Assert.Contains("code01-erin", done);
        Assert.Equal(1, _host.Directory.User("erin").CompleteEnrollmentCalls);
    }

    [Fact]
    public async Task Replace_WithoutMfa_RedirectsToTheOverview()
    {
        var browser = await SignedInAsync("alice");

        Assert.Equal(AccountPath, (await browser.GetAsync(ReplacePath)).Headers.Location!.OriginalString);
    }

    // --- New recovery codes ---

    [Fact]
    public async Task RecoveryCodes_WrongCode_IsAnsweredGenerically()
    {
        var browser = await SignedInAsync("erin");

        var html = await HtmlAsync(await PostAsync(browser, RecoveryCodesPath, new() { ["Code"] = "erin1-recov" }));

        Assert.Contains(WrongCode, html);
        Assert.Equal(["erin1-recov"], _host.Directory.User("erin").RecoveryCodes);
        Assert.Equal(OutcomeReason.InvalidSecondFactor, Assert.Single(Events(SecurityEvent.StepUpFailed)).Reason);
        Assert.Empty(Events(SecurityEvent.RecoveryCodesRegenerated));
    }

    [Fact]
    public async Task RecoveryCodes_RightCode_ShowsTheNewCodesOnce()
    {
        var browser = await SignedInAsync("erin");

        var html = await HtmlAsync(await PostAsync(browser, RecoveryCodesPath, new() { ["Code"] = FakeDirectory.TotpCode }));

        Assert.Contains("new01-erin", html);
        Assert.Contains("new10-erin", html);
        Assert.DoesNotContain("new01-erin", await HtmlAsync(await browser.GetAsync(RecoveryCodesPath)));
        var erin = _host.Directory.User("erin");
        var sid = Assert.Single(await _host.SessionsOfAsync(erin.UserId)).Id;
        AssertUserChange(Assert.Single(Events(SecurityEvent.RecoveryCodesRegenerated)), erin, sid);
        Assert.Null((await _host.FindSessionAsync(sid))!.EndedAt);
    }

    // --- Turn off two-step verification ---

    [Fact]
    public async Task Remove_WithoutConfirming_IsRefused()
    {
        var browser = await SignedInAsync("erin");

        var html = await HtmlAsync(await PostAsync(browser, RemovePath, new() { ["Code"] = FakeDirectory.TotpCode }));

        Assert.Contains("Please confirm", html);
        Assert.True(_host.Directory.User("erin").HasFactor);
    }

    [Fact]
    public async Task Remove_WrongCode_IsAnsweredGenerically()
    {
        var browser = await SignedInAsync("erin");

        var html = await HtmlAsync(await PostAsync(browser, RemovePath, new() { ["Code"] = "000000", ["Confirm"] = "true" }));

        Assert.Contains(WrongCode, html);
        Assert.True(_host.Directory.User("erin").HasFactor);
        Assert.Equal(OutcomeReason.InvalidSecondFactor, Assert.Single(Events(SecurityEvent.StepUpFailed)).Reason);
        Assert.Empty(Events(SecurityEvent.MfaRemoved));
    }

    [Fact]
    public async Task Remove_CodeAndConfirm_TurnsMfaOff()
    {
        var browser = await SignedInAsync("erin");

        var html = await HtmlAsync(await PostAsync(browser, RemovePath, new() { ["Code"] = FakeDirectory.TotpCode, ["Confirm"] = "true" }));

        Assert.Contains("Two-step verification is off", html);
        Assert.False(_host.Directory.User("erin").HasFactor);
        var erin = _host.Directory.User("erin");
        var sid = Assert.Single(await _host.SessionsOfAsync(erin.UserId)).Id;
        AssertUserChange(Assert.Single(Events(SecurityEvent.MfaRemoved)), erin, sid);
    }

    [Fact]
    public async Task Remove_MfaRequired_PostIsRedirected_AndChangesNothing()
    {
        var browser = await SignedInAsync("erin");
        var page = await HtmlAsync(await browser.GetAsync(RemovePath));
        _host.Directory.MfaRequired = true;

        var response = await PostFromAsync(browser, page, RemovePath, new() { ["Code"] = FakeDirectory.TotpCode, ["Confirm"] = "true" });

        Assert.Equal(AccountPath, response.Headers.Location!.OriginalString);
        Assert.True(_host.Directory.User("erin").HasFactor);
    }

    // --- Authenticator replaced or removed: the user's other sessions end, this one stays ---

    /// <summary>erin signed in on two browsers: the one she acts from, and another (say, a stolen session).</summary>
    private async Task<(BrowserClient Current, Guid CurrentSid, BrowserClient Other, Guid OtherSid)> TwoSessionsAsync()
    {
        var erin = _host.Directory.User("erin").UserId;
        var other = await SignedInAsync("erin");
        var otherSid = Assert.Single(await _host.SessionsOfAsync(erin)).Id;
        var current = await SignedInAsync("erin");
        var currentSid = Assert.Single(await _host.SessionsOfAsync(erin), s => s.Id != otherSid).Id;
        return (current, currentSid, other, otherSid);
    }

    private async Task AssertOnlyCurrentSessionLivesAsync(BrowserClient current, Guid currentSid, BrowserClient other, Guid otherSid)
    {
        Assert.Null((await _host.FindSessionAsync(currentSid))!.EndedAt);
        var ended = (await _host.FindSessionAsync(otherSid))!;
        Assert.NotNull(ended.EndedAt);
        Assert.Equal(EndReason.MfaChanged, ended.EndReason);

        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync(AccountPath)).StatusCode);
        Assert.Equal(LoginFor(AccountPath), (await other.GetAsync(AccountPath)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Replace_Completed_EndsTheUsersOtherSessions_AndKeepsThisOne()
    {
        var (current, currentSid, other, otherSid) = await TwoSessionsAsync();

        var started = await HtmlAsync(await PostAsync(current, ReplacePath,
            new() { ["CurrentCode"] = FakeDirectory.TotpCode }, handler: "Start"));
        // Starting a replacement changes nothing yet.
        Assert.Null((await _host.FindSessionAsync(otherSid))!.EndedAt);

        var done = await HtmlAsync(await PostFromAsync(current, started, ReplacePath,
            new() { ["State"] = State(started), ["Code"] = FakeDirectory.TotpCode }, handler: "Verify"));

        Assert.Contains("signed out everywhere else", done);
        await AssertOnlyCurrentSessionLivesAsync(current, currentSid, other, otherSid);
        var erin = _host.Directory.User("erin");
        AssertUserChange(Assert.Single(Events(SecurityEvent.AuthenticatorReplaced)), erin, currentSid);
        Assert.Empty(Events(SecurityEvent.TotpEnrolled));
        Assert.All(
            _host.ActorScope.Observed.Where(o => o.Request is "BeginTotpReplacement" or "CompleteTotpEnrollment"),
            o => Assert.Equal((erin.TenantId, erin.UserId), o.ActingAs));
    }

    [Fact]
    public async Task Remove_EndsTheUsersOtherSessions_AndKeepsThisOne()
    {
        var (current, currentSid, other, otherSid) = await TwoSessionsAsync();

        var html = await HtmlAsync(await PostAsync(current, RemovePath,
            new() { ["Code"] = FakeDirectory.TotpCode, ["Confirm"] = "true" }));

        Assert.Contains("signed out everywhere else", html);
        await AssertOnlyCurrentSessionLivesAsync(current, currentSid, other, otherSid);
        var erin = _host.Directory.User("erin");
        AssertUserChange(Assert.Single(Events(SecurityEvent.MfaRemoved)), erin, currentSid);
        var (_, actingAs) = Assert.Single(_host.ActorScope.Observed, o => o.Request == "RemoveMfa");
        Assert.Equal((erin.TenantId, erin.UserId), actingAs);
    }

    [Fact]
    public async Task Remove_WrongCode_EndsNoSession()
    {
        var (current, currentSid, _, otherSid) = await TwoSessionsAsync();

        await PostAsync(current, RemovePath, new() { ["Code"] = "000000", ["Confirm"] = "true" });

        Assert.Null((await _host.FindSessionAsync(currentSid))!.EndedAt);
        Assert.Null((await _host.FindSessionAsync(otherSid))!.EndedAt);
    }

    [Fact]
    public async Task AuditEvents_NeverCarryWhatTheUserTyped_OrTheCodesIssued()
    {
        var browser = await SignedInAsync("erin");
        await PostAsync(browser, RecoveryCodesPath, new() { ["Code"] = "erin1-recov" });
        await PostAsync(browser, RecoveryCodesPath, new() { ["Code"] = FakeDirectory.TotpCode });
        await PostAsync(browser, PasswordPath, new()
        {
            ["CurrentPassword"] = "a wrong guess", ["NewPassword"] = "a brand new password", ["ConfirmPassword"] = "a brand new password"
        });

        var serialized = JsonSerializer.Serialize(_host.AuditLog.Events);
        Assert.Equal(
            [SecurityEvent.StepUpFailed, SecurityEvent.RecoveryCodesRegenerated, SecurityEvent.StepUpFailed],
            _host.AuditLog.Events.Where(e => e.Event != SecurityEvent.SessionStarted).Select(e => e.Event));
        foreach (var secret in new[] { "erin1-recov", "new01-erin", "a wrong guess", "a brand new password", FakeDirectory.UserPassword })
            Assert.DoesNotContain(secret, serialized);
    }

    // --- Cross-cutting ---

    [Theory]
    [InlineData(PasswordPath, null)]
    [InlineData(SetupPath, "Start")]
    [InlineData(RecoveryCodesPath, null)]
    [InlineData(RemovePath, null)]
    public async Task Posts_WithoutAntiforgery_AreRejected(string path, string? handler)
    {
        var browser = await SignedInAsync("erin");

        var response = await browser.PostFormAsync(handler is null ? path : $"{path}?handler={handler}",
            new Dictionary<string, string> { ["Code"] = FakeDirectory.TotpCode, ["Confirm"] = "true" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(_host.Directory.User("erin").HasFactor);
    }

    private static string FormFieldValues(string html) =>
        string.Join('\n', InputValuePattern().Matches(html).Select(m => m.Groups[1].Value));

    [GeneratedRegex("<input[^>]*value=\"([^\"]*)\"")]
    private static partial Regex InputValuePattern();

    [GeneratedRegex("name=\"State\" value=\"([^\"]+)\"")]
    private static partial Regex StatePattern();

    private IReadOnlyList<RecordAuditEventCommand> Events(SecurityEvent securityEvent) =>
        _host.AuditLog.Events.Where(e => e.Event == securityEvent).ToList();

    private static void AssertUserChange(RecordAuditEventCommand recorded, FakeDirectory.UserEntry user, Guid sid)
    {
        Assert.Equal(user.TenantId, recorded.TenantId);
        Assert.Equal(Actor.User(user.UserId), recorded.Actor);
        Assert.Equal([Target.User(user.UserId)], recorded.Targets);
        Assert.Null(recorded.Reason);
        Assert.Equal(sid, recorded.Session?.Sid);
    }

    private static void AssertStepUpFailed(
        RecordAuditEventCommand recorded, FakeDirectory.UserEntry user, Guid sid, OutcomeReason reason)
    {
        Assert.Equal(SecurityEvent.StepUpFailed, recorded.Event);
        Assert.Equal(reason, recorded.Reason);
        Assert.Equal(user.TenantId, recorded.TenantId);
        Assert.Equal(Actor.User(user.UserId), recorded.Actor);
        Assert.Equal([Target.User(user.UserId)], recorded.Targets);
        Assert.Equal(sid, recorded.Session?.Sid);
    }
}
