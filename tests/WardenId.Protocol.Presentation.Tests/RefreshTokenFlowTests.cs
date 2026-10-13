using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;

namespace WardenId.Protocol.Presentation.Tests;

public class RefreshTokenFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> CodeTokensAsync(
        ProtocolTestHost host, string clientId = "web-rt", string? secret = FakeDirectory.AcmeSecret,
        string redirectUri = FakeDirectory.WebRedirect, string scope = "openid profile email offline_access")
    {
        var pkce = OidcFlow.NewPkce();
        var code = await OidcFlow.CodeAsync(host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters(clientId, redirectUri, pkce, scope), redirectUri);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = pkce.Verifier
        };
        if (secret is not null) form["client_secret"] = secret;
        var response = await host.Client.PostAsync("/tenants/acme/token", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    private static Task<HttpResponseMessage> RefreshAsync(
        ProtocolTestHost host, string refreshToken, string clientId = "web-rt", string? secret = FakeDirectory.AcmeSecret,
        string slug = "acme", string? scope = null)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken
        };
        if (secret is not null) form["client_secret"] = secret;
        if (scope is not null) form["scope"] = scope;
        return host.Client.PostAsync($"/tenants/{slug}/token", new FormUrlEncodedContent(form));
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string error, string? description = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(error, body.GetProperty("error").GetString());
        if (description is not null)
            Assert.Equal(description, body.GetProperty("error_description").GetString());
    }

    private static string RefreshTokenOf(JsonElement body) => body.GetProperty("refresh_token").GetString()!;

    [Fact]
    public async Task OfflineAccess_CodeRedemption_ReturnsRefreshToken()
    {
        var body = await CodeTokensAsync(_host);

        Assert.False(string.IsNullOrEmpty(RefreshTokenOf(body)));
    }

    [Fact]
    public async Task WithoutOfflineAccess_NoRefreshToken()
    {
        var body = await CodeTokensAsync(_host, scope: "openid");

        Assert.False(body.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task Refresh_RotatesAndIssuesTenantSignedTokens_WithFreshClaims()
    {
        var acme = _host.Directory.Tenant("acme");
        var alice = _host.Directory.User("alice");
        var rt1 = RefreshTokenOf(await CodeTokensAsync(_host));

        var response = await RefreshAsync(_host, rt1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.NotEqual(rt1, RefreshTokenOf(body));
        var access = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("access_token").GetString()!);
        Assert.Equal("ES256", access.Alg);
        Assert.Equal(acme.KeyId.ToString("N"), access.Kid);
        Assert.Equal("https://wardenid.test/tenants/acme", access.Issuer);
        Assert.Equal(alice.UserId.ToString(), access.Subject);
        var id = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("id_token").GetString()!);
        Assert.Equal(alice.Email, id.GetClaim("email").Value);
        // auth_time survives rotation unchanged (the absolute cap's anchor).
        Assert.True(id.TryGetClaim("auth_time", out _));
    }

    // Review focus 1: replay kills the whole family, including the current token.
    [Fact]
    public async Task ReplayOfRotatedToken_IsInvalidGrant_AndRevokesCurrentToken()
    {
        var rt1 = RefreshTokenOf(await CodeTokensAsync(_host));
        var rt2 = RefreshTokenOf(await JsonAsync(await RefreshAsync(_host, rt1)));

        await AssertErrorAsync(await RefreshAsync(_host, rt1), "invalid_grant");
        await AssertErrorAsync(await RefreshAsync(_host, rt2), "invalid_grant");
    }

    [Fact]
    public async Task RefreshTokenFromOtherTenant_IsInvalidGrant()
    {
        var rt = RefreshTokenOf(await CodeTokensAsync(_host));

        await AssertErrorAsync(
            await RefreshAsync(_host, rt, secret: FakeDirectory.GlobexSecret, slug: "globex"),
            "invalid_grant", "The refresh token is not valid.");
    }

    [Fact]
    public async Task RefreshTokenPresentedByOtherClient_IsInvalidGrant()
    {
        var rt = RefreshTokenOf(await CodeTokensAsync(_host));

        await AssertErrorAsync(await RefreshAsync(_host, rt, clientId: "web-rt-other"), "invalid_grant");
    }

    [Fact]
    public async Task UserDisabledAfterIssue_IsInvalidGrant()
    {
        var rt = RefreshTokenOf(await CodeTokensAsync(_host));
        _host.Directory.User("alice").Status = UserStatus.Disabled;

        await AssertErrorAsync(await RefreshAsync(_host, rt), "invalid_grant", "The user can no longer sign in.");
    }

    // Review focus 2.
    [Fact]
    public async Task ConfidentialClient_WithBasicAuth_CanRefresh()
    {
        var rt = RefreshTokenOf(await CodeTokensAsync(_host));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/tenants/acme/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = rt
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"web-rt:{FakeDirectory.AcmeSecret}")));

        var response = await _host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Review focus 3.
    [Fact]
    public async Task PublicClient_CanRefresh_WithoutSecret()
    {
        var rt = RefreshTokenOf(await CodeTokensAsync(_host, "spa-rt", secret: null, redirectUri: FakeDirectory.SpaRtRedirect));

        var response = await RefreshAsync(_host, rt, clientId: "spa-rt", secret: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Review focus 4.
    [Fact]
    public async Task Refresh_NarrowerScope_Succeeds_BroaderScope_IsInvalidScope()
    {
        var rt = RefreshTokenOf(await CodeTokensAsync(_host, scope: "openid email offline_access"));

        await AssertErrorAsync(await RefreshAsync(_host, rt, scope: "openid email profile offline_access"), "invalid_scope");

        var narrowed = await RefreshAsync(_host, rt, scope: "openid offline_access");
        Assert.Equal(HttpStatusCode.OK, narrowed.StatusCode);
        var id = new JsonWebTokenHandler().ReadJsonWebToken((await JsonAsync(narrowed)).GetProperty("id_token").GetString()!);
        Assert.False(id.TryGetClaim("email", out _));
    }

    // RFC 6749 §6: narrowing applies to the access token only; the rotated refresh token keeps the full grant.
    [Fact]
    public async Task NarrowedRefresh_ThenRefreshWithoutScope_RestoresOriginalGrant()
    {
        var rt1 = RefreshTokenOf(await CodeTokensAsync(_host, scope: "openid email offline_access"));
        var rt2 = RefreshTokenOf(await JsonAsync(await RefreshAsync(_host, rt1, scope: "openid offline_access")));

        var response = await RefreshAsync(_host, rt2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        var id = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("id_token").GetString()!);
        Assert.Equal(_host.Directory.User("alice").Email, id.GetClaim("email").Value);
        Assert.Contains("email", body.GetProperty("scope").GetString()!.Split(' '));
    }

    [Fact]
    public async Task NarrowedRefresh_WithoutOfflineAccess_StillRotatesRefreshToken()
    {
        var rt1 = RefreshTokenOf(await CodeTokensAsync(_host, scope: "openid email offline_access"));

        var response = await RefreshAsync(_host, rt1, scope: "openid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rt2 = RefreshTokenOf(await JsonAsync(response));
        Assert.False(string.IsNullOrEmpty(rt2));
        // The rotated token is usable: the session survives the narrowed request.
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_host, rt2)).StatusCode);
    }

    [Fact]
    public async Task NarrowedRefresh_AccessTokenCarriesNarrowedScopes()
    {
        var rt1 = RefreshTokenOf(await CodeTokensAsync(_host, scope: "openid email offline_access"));

        var response = await RefreshAsync(_host, rt1, scope: "openid offline_access");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        var access = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("access_token").GetString()!);
        Assert.Equal(["offline_access", "openid"], access.GetClaim("scope").Value.Split(' ').Order());
        // OpenIddict omits `scope` when it equals the requested set; if present it must be the narrowed one.
        if (body.TryGetProperty("scope", out var scope))
            Assert.Equal(["offline_access", "openid"], scope.GetString()!.Split(' ').Order());
    }

    // Review focus 5.
    [Fact]
    public async Task RefreshGrantRemovedAfterIssue_IsUnauthorizedClient()
    {
        var rt = RefreshTokenOf(await CodeTokensAsync(_host));
        var clients = _host.Directory.Clients;
        var index = clients.FindIndex(c => c.TenantId == _host.Directory.Tenant("acme").TenantId && c.ClientId == "web-rt");
        clients[index] = clients[index] with { Grants = [AllowedGrant.AuthorizationCode] };

        await AssertErrorAsync(await RefreshAsync(_host, rt), "unauthorized_client");
    }

    // Grant removed between /authorize and code redemption: the code still redeems, but without a refresh token.
    [Fact]
    public async Task RefreshGrantRemovedBeforeCodeRedemption_IssuesNoRefreshToken()
    {
        var pkce = OidcFlow.NewPkce();
        var code = await OidcFlow.CodeAsync(_host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters("web-rt", FakeDirectory.WebRedirect, pkce, "openid offline_access"),
            FakeDirectory.WebRedirect);
        var clients = _host.Directory.Clients;
        var index = clients.FindIndex(c => c.TenantId == _host.Directory.Tenant("acme").TenantId && c.ClientId == "web-rt");
        clients[index] = clients[index] with { Grants = [AllowedGrant.AuthorizationCode] };

        var response = await _host.Client.PostAsync("/tenants/acme/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "web-rt",
                ["client_secret"] = FakeDirectory.AcmeSecret,
                ["code"] = code,
                ["redirect_uri"] = FakeDirectory.WebRedirect,
                ["code_verifier"] = pkce.Verifier
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await JsonAsync(response)).TryGetProperty("refresh_token", out _));
    }

    // auth_time is stamped at login from the wall clock (second precision), so every step is placed relative to
    // the id_token's auth_time: the clamped expiry then lands exactly on the cap.
    private static async Task<(ProtocolTestHost Host, FakeTimeProvider Clock, DateTimeOffset Cap, string Rt2)>
        RotateNearAbsoluteCapAsync()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        var host = await ProtocolTestHost.StartAsync(clock, new Dictionary<string, string?>
        {
            ["ProtocolToken:RefreshTokenAbsoluteLifetime"] = "01:00:00"
        });
        var tokens = await CodeTokensAsync(host);
        var id = new JsonWebTokenHandler().ReadJsonWebToken(tokens.GetProperty("id_token").GetString()!);
        var cap = DateTimeOffset.FromUnixTimeSeconds(long.Parse(id.GetClaim("auth_time").Value)).AddHours(1);

        clock.SetUtcNow(cap - TimeSpan.FromMinutes(1));
        var second = await RefreshAsync(host, RefreshTokenOf(tokens));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        return (host, clock, cap, RefreshTokenOf(await JsonAsync(second)));
    }

    [Fact]
    public async Task AbsoluteCap_ClampsRotatedTokenLifetime_EndToEnd()
    {
        var (host, clock, cap, rt2) = await RotateNearAbsoluteCapAsync();
        await using var _ = host;

        // Without the clamp RT2 would live 30 days and reach the factory ("The session has expired.");
        // clamped, OpenIddict itself rejects it as expired one second past the cap.
        clock.SetUtcNow(cap + TimeSpan.FromSeconds(1));
        await AssertErrorAsync(await RefreshAsync(host, rt2), "invalid_grant", "The specified refresh token is no longer valid.");
    }

    [Fact]
    public async Task AbsoluteCap_ReachedExactly_IsSessionExpired()
    {
        var (host, clock, cap, rt2) = await RotateNearAbsoluteCapAsync();
        await using var _ = host;

        // At exactly exp OpenIddict still accepts the token (zero clock skew, strict <); the factory's cap check
        // (remaining <= 0) is what rejects it.
        clock.SetUtcNow(cap);
        await AssertErrorAsync(await RefreshAsync(host, rt2), "invalid_grant", "The session has expired.");
    }
}
