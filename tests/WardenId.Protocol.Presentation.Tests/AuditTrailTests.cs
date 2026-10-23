using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Utilities;
using static WardenId.Protocol.Presentation.Tests.TokenFlow;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>What the protocol flows send to the Audit module (W24), end to end through the real handlers.</summary>
public class AuditTrailTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Guid AcmeId => _host.Directory.Tenant("acme").TenantId;

    private Guid AliceId => _host.Directory.User("alice").UserId;

    private IReadOnlyList<RecordAuditEventCommand> Events(SecurityEvent securityEvent) =>
        _host.AuditLog.Events.Where(e => e.Event == securityEvent).ToList();

    private static Task<HttpResponseMessage> PostLoginAsync(BrowserClient browser, string user, string password) =>
        PostLoginAsync(browser, "acme", user, password);

    private static async Task<HttpResponseMessage> PostLoginAsync(BrowserClient browser, string slug, string user, string password)
    {
        var loginUrl = $"/tenants/{slug}/login";
        var page = await browser.GetAsync(loginUrl);
        var token = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        return await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        });
    }

    private static Guid SidOf(string idToken) =>
        Guid.Parse(new JsonWebTokenHandler().ReadJsonWebToken(idToken).GetClaim(SessionClaimTypes.SessionId).Value);

    private static string JtiOf(string jwt) => new JsonWebTokenHandler().ReadJsonWebToken(jwt).Id;

    [Fact]
    public async Task SignIn_RecordsTheSessionStart_WithSidAndAuthTime()
    {
        var tokens = await CodeTokensAsync(_host);
        var sid = SidOf(tokens.GetProperty("id_token").GetString()!);

        var started = Assert.Single(Events(SecurityEvent.SessionStarted));
        Assert.Equal(AcmeId, started.TenantId);
        Assert.Equal(Actor.User(AliceId), started.Actor);
        Assert.Equal([Target.User(AliceId), Target.Session(sid)], started.Targets);
        Assert.Equal(sid, started.Session!.Sid);
        Assert.NotNull(started.Session.AuthTime);
        Assert.Equal(["pwd"], started.Session.Amr);
        Assert.Empty(Events(SecurityEvent.SignInFailed));
    }

    [Fact]
    public async Task SignInWithUnknownUser_IsAnonymousFailure()
    {
        await PostLoginAsync(_host.CreateBrowser(), "nobody", FakeDirectory.UserPassword);

        var failed = Assert.Single(Events(SecurityEvent.SignInFailed));
        Assert.Equal(AcmeId, failed.TenantId);
        Assert.Equal(Actor.Anonymous, failed.Actor);
        Assert.Empty(failed.Targets);
        Assert.Equal(OutcomeReason.UnknownUser, failed.Reason);
        Assert.Empty(Events(SecurityEvent.SessionStarted));
    }

    [Theory]
    [InlineData("alice", "wrong", OutcomeReason.InvalidPassword)]
    [InlineData("carol", FakeDirectory.UserPassword, OutcomeReason.AccountDisabled)]
    [InlineData("bob", FakeDirectory.UserPassword, OutcomeReason.EmailNotVerified)]
    public async Task SignInFailure_RecordsTheReason_AgainstTheUser(string username, string password, OutcomeReason reason)
    {
        await PostLoginAsync(_host.CreateBrowser(), username, password);

        var userId = _host.Directory.User(username).UserId;
        var failed = Assert.Single(Events(SecurityEvent.SignInFailed));
        Assert.Equal(reason, failed.Reason);
        Assert.Equal(Actor.User(userId), failed.Actor);
        Assert.Equal([Target.User(userId)], failed.Targets);
        Assert.Empty(Events(SecurityEvent.SessionStarted));
    }

    [Fact]
    public async Task CodeRedemption_RecordsTheIssuance_ForTheClient_WithTheUsersSession()
    {
        var tokens = await CodeTokensAsync(_host);
        var sid = SidOf(tokens.GetProperty("id_token").GetString()!);

        var issued = Assert.Single(Events(SecurityEvent.TokenIssuedAuthorizationCode));
        Assert.Equal(AcmeId, issued.TenantId);
        Assert.Equal(Actor.Client("web-rt"), issued.Actor);
        Assert.Contains(Target.Client("web-rt"), issued.Targets);
        Assert.Contains(Target.User(AliceId), issued.Targets);
        Assert.Contains(issued.Targets, t => t.Kind == TargetKind.TokenFamily);
        Assert.Equal(sid, issued.Session!.Sid);
        Assert.NotNull(issued.Session.AuthTime);
        Assert.Equal(JtiOf(tokens.GetProperty("access_token").GetString()!), issued.Session.TokenId);
    }

    [Fact]
    public async Task Refresh_RecordsTheIssuance_InTheSameFamily()
    {
        var tokens = await CodeTokensAsync(_host);

        var response = await RefreshAsync(_host, tokens.GetProperty("refresh_token").GetString()!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = Assert.Single(Events(SecurityEvent.TokenIssuedAuthorizationCode));
        var refresh = Assert.Single(Events(SecurityEvent.TokenIssuedRefreshToken));
        Assert.Equal(Actor.Client("web-rt"), refresh.Actor);
        Assert.Equal(
            code.Targets.Single(t => t.Kind == TargetKind.TokenFamily),
            refresh.Targets.Single(t => t.Kind == TargetKind.TokenFamily));
    }

    [Fact]
    public async Task ClientCredentials_RecordsAMachineIssuance_WithoutUser()
    {
        var tokens = await ClientCredentialsAsync(_host);

        var issued = Assert.Single(Events(SecurityEvent.TokenIssuedClientCredentials));
        Assert.Equal(AcmeId, issued.TenantId);
        Assert.Equal(Actor.Client("svc"), issued.Actor);
        Assert.Equal([Target.Client("svc")], issued.Targets);
        Assert.Equal(JtiOf(tokens.GetProperty("access_token").GetString()!), issued.Session!.TokenId);
    }

    [Fact]
    public async Task FailedTokenRequest_RecordsNoIssuance()
    {
        var response = await _host.Client.PostAsync("/tenants/acme/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "svc",
            ["client_secret"] = "wrong"
        }));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Events(SecurityEvent.TokenIssuedClientCredentials));
    }

    [Fact]
    public async Task RevokingARefreshToken_RecordsTheRevocation_OfItsFamily()
    {
        var tokens = await CodeTokensAsync(_host);
        var family = Assert.Single(Events(SecurityEvent.TokenIssuedAuthorizationCode)).Targets.Single(t => t.Kind == TargetKind.TokenFamily);

        var response = await RevokeAsync(_host, tokens.GetProperty("refresh_token").GetString()!, hint: "refresh_token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var revoked = Assert.Single(Events(SecurityEvent.TokenRevoked));
        Assert.Equal(AcmeId, revoked.TenantId);
        Assert.Equal(Actor.Client("web-rt"), revoked.Actor);
        Assert.Contains(Target.User(AliceId), revoked.Targets);
        Assert.Contains(family, revoked.Targets);
        Assert.False(string.IsNullOrEmpty(revoked.Session!.TokenId));
    }

    [Fact]
    public async Task RevokingAnUnknownToken_RecordsNothing()
    {
        await RevokeAsync(_host, "not-a-token");

        Assert.Empty(Events(SecurityEvent.TokenRevoked));
    }

    [Fact]
    public async Task RefreshTokenReplay_RecordsTheReplay_AndTheRevokedFamily()
    {
        var tokens = await CodeTokensAsync(_host);
        var rt1 = tokens.GetProperty("refresh_token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_host, rt1)).StatusCode);
        var family = Assert.Single(Events(SecurityEvent.TokenIssuedAuthorizationCode)).Targets.Single(t => t.Kind == TargetKind.TokenFamily);

        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(_host, rt1)).StatusCode);

        var replay = Assert.Single(Events(SecurityEvent.RefreshTokenReplayed));
        Assert.Equal(AcmeId, replay.TenantId);
        Assert.Equal(Actor.Client("web-rt"), replay.Actor);
        Assert.Contains(Target.User(AliceId), replay.Targets);
        Assert.Contains(family, replay.Targets);
        Assert.False(string.IsNullOrEmpty(replay.Session!.TokenId));
        Assert.Single(Events(SecurityEvent.TokenIssuedRefreshToken));
    }

    [Fact]
    public async Task OrdinaryRefresh_IsNotAReplay()
    {
        var tokens = await CodeTokensAsync(_host);

        await RefreshAsync(_host, tokens.GetProperty("refresh_token").GetString()!);

        Assert.Empty(Events(SecurityEvent.RefreshTokenReplayed));
    }

    [Fact]
    public async Task RpInitiatedLogout_RecordsTheSignOut_ByTheUser_ForTheClient()
    {
        var browser = _host.CreateBrowser();
        var tokens = await CodeTokensAsync(_host, browser: browser);
        var idToken = tokens.GetProperty("id_token").GetString()!;
        var sid = SidOf(idToken);

        var response = await browser.GetAsync(QueryHelpers.AddQueryString("/tenants/acme/endsession", new Dictionary<string, string?>
        {
            ["id_token_hint"] = idToken,
            ["client_id"] = "web-rt",
            ["post_logout_redirect_uri"] = FakeDirectory.WebLoggedOut
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var signedOut = Assert.Single(Events(SecurityEvent.SignedOut));
        Assert.Equal(AcmeId, signedOut.TenantId);
        Assert.Equal(Actor.User(AliceId), signedOut.Actor);
        Assert.Equal([Target.User(AliceId), Target.Session(sid), Target.Client("web-rt")], signedOut.Targets);
        Assert.Equal(sid, signedOut.Session!.Sid);
    }

    [Fact]
    public async Task LogoutWithoutASession_RecordsNothing()
    {
        await _host.CreateBrowser().GetAsync("/tenants/acme/endsession");

        Assert.Empty(Events(SecurityEvent.SignedOut));
    }
}
