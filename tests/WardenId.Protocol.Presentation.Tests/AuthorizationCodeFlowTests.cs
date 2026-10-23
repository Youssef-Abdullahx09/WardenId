using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Protocol.Application.Utilities;
using WardenId.SharedKernel.Application.Utilities;

namespace WardenId.Protocol.Presentation.Tests;

public class AuthorizationCodeFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<HttpResponseMessage> RedeemAsync(
        string slug, string clientId, string? secret, string code, string redirectUri, string? verifier)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        };
        if (secret is not null) form["client_secret"] = secret;
        if (verifier is not null) form["code_verifier"] = verifier;
        return _host.Client.PostAsync($"/tenants/{slug}/token", new FormUrlEncodedContent(form));
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<(string Code, string Verifier)> WebCodeAsync(string scope = "openid profile email")
    {
        var pkce = OidcFlow.NewPkce();
        var code = await OidcFlow.CodeAsync(_host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, pkce, scope), FakeDirectory.WebRedirect);
        return (code, pkce.Verifier);
    }

    private static async Task AssertInvalidGrantAsync(HttpResponseMessage response, string? expectedDescription = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
        if (expectedDescription is not null)
            Assert.Equal(expectedDescription, body.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task ConfidentialClient_RedeemsCode_ForTenantSignedTokens()
    {
        var (code, verifier) = await WebCodeAsync();
        var acme = _host.Directory.Tenant("acme");
        var alice = _host.Directory.User("alice");

        var response = await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, verifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        // No offline_access requested.
        Assert.False(body.TryGetProperty("refresh_token", out _));

        var access = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("access_token").GetString()!);
        Assert.Equal("ES256", access.Alg);
        Assert.Equal(acme.KeyId.ToString("N"), access.Kid);
        Assert.Equal("https://wardenid.test/tenants/acme", access.Issuer);
        Assert.Equal(new[] { "https://wardenid.test/tenants/acme/api" }, access.Audiences);
        Assert.Equal(alice.UserId.ToString(), access.Subject);
        Assert.Equal("web", access.GetClaim("client_id").Value);
        Assert.Equal(acme.TenantId.ToString(), access.GetClaim(CustomClaimTypes.TenantId).Value);
        Assert.Equal(TokenUse.Access, access.GetClaim(CustomClaimTypes.TokenUse).Value);
        Assert.False(access.TryGetClaim("email", out _));

        var id = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("id_token").GetString()!);
        Assert.Equal("ES256", id.Alg);
        Assert.Equal(acme.KeyId.ToString("N"), id.Kid);
        Assert.Equal("https://wardenid.test/tenants/acme", id.Issuer);
        Assert.Equal(new[] { "web" }, id.Audiences);
        Assert.Equal(alice.UserId.ToString(), id.Subject);
        Assert.Equal("n-123", id.GetClaim("nonce").Value);
        Assert.Equal("alice", id.GetClaim("preferred_username").Value);
        Assert.Equal(alice.Email, id.GetClaim("email").Value);
        var rawPayload = JsonDocument.Parse(
            Base64UrlEncoder.Decode(body.GetProperty("id_token").GetString()!.Split('.')[1])).RootElement;
        Assert.Equal(JsonValueKind.True, rawPayload.GetProperty("email_verified").ValueKind);
        Assert.Equal(JsonValueKind.Number, rawPayload.GetProperty("auth_time").ValueKind);
        Assert.False(id.TryGetClaim(CustomClaimTypes.TokenUse, out _));
    }

    [Fact]
    public async Task ClientIdTokenLifetimeOverride_SetsIdTokenExpiry_AndLeavesAccessTokenAtDefault()
    {
        var pkce = OidcFlow.NewPkce();
        var code = await OidcFlow.CodeAsync(_host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters("web-short-id", FakeDirectory.WebRedirect, pkce, "openid"), FakeDirectory.WebRedirect);

        var response = await RedeemAsync(
            "acme", "web-short-id", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, pkce.Verifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        var id = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("id_token").GetString()!);
        Assert.Equal(TimeSpan.FromSeconds(120), id.ValidTo - id.IssuedAt);
        var access = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("access_token").GetString()!);
        Assert.Equal(TimeSpan.FromMinutes(15), access.ValidTo - access.IssuedAt);
    }

    [Fact]
    public async Task OpenIdOnly_IdTokenHasNoProfileOrEmailClaims()
    {
        var (code, verifier) = await WebCodeAsync("openid");

        var response = await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, verifier);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);

        var id = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("id_token").GetString()!);
        Assert.False(id.TryGetClaim("email", out _));
        Assert.False(id.TryGetClaim("email_verified", out _));
        Assert.False(id.TryGetClaim("preferred_username", out _));
    }

    [Fact]
    public async Task PublicClient_RedeemsWithVerifier_WithoutSecret()
    {
        var pkce = OidcFlow.NewPkce();
        var code = await OidcFlow.CodeAsync(_host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters("spa", FakeDirectory.SpaRedirect, pkce), FakeDirectory.SpaRedirect);

        var response = await RedeemAsync("acme", "spa", null, code, FakeDirectory.SpaRedirect, pkce.Verifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConfidentialClient_WithoutPkce_IsAllowed()
    {
        var code = await OidcFlow.CodeAsync(_host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters("web", FakeDirectory.WebRedirect, pkce: null), FakeDirectory.WebRedirect);

        var response = await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, verifier: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WrongVerifier_IsInvalidGrant()
    {
        var (code, _) = await WebCodeAsync();

        await AssertInvalidGrantAsync(
            await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, OidcFlow.NewPkce().Verifier));
    }

    [Fact]
    public async Task WrongRedirectUri_IsInvalidGrant()
    {
        var (code, verifier) = await WebCodeAsync();

        await AssertInvalidGrantAsync(
            await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, "https://app.example/other", verifier));
    }

    [Fact]
    public async Task ReplayedCode_IsInvalidGrant()
    {
        var (code, verifier) = await WebCodeAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, verifier)).StatusCode);

        await AssertInvalidGrantAsync(
            await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, verifier));
    }

    // Review focus: same client_id "web" exists in both tenants.
    [Fact]
    public async Task CodeFromOtherTenant_IsInvalidGrant()
    {
        var (code, verifier) = await WebCodeAsync();

        await AssertInvalidGrantAsync(
            await RedeemAsync("globex", "web", FakeDirectory.GlobexSecret, code, FakeDirectory.WebRedirect, verifier),
            "The authorization code is not valid.");
    }

    [Fact]
    public async Task UserDisabledAfterAuthorize_IsInvalidGrant()
    {
        var (code, verifier) = await WebCodeAsync();
        _host.Directory.User("alice").Status = UserStatus.Disabled;

        await AssertInvalidGrantAsync(
            await RedeemAsync("acme", "web", FakeDirectory.AcmeSecret, code, FakeDirectory.WebRedirect, verifier));
    }
}
