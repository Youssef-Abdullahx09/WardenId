using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using WardenId.Protocol.Application;
using WardenId.SharedKernel.Application;

namespace WardenId.Protocol.Presentation.Tests;

public class TokenEndpointTests : IAsyncLifetime
{
    private TokenTestHost _host = null!;

    public async Task InitializeAsync() => _host = await TokenTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<HttpResponseMessage> PostTokenAsync(
        string slug, Dictionary<string, string> form, (string Id, string Secret)? basic = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/tenants/{slug}/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        if (basic is { } b)
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{Uri.EscapeDataString(b.Id)}:{Uri.EscapeDataString(b.Secret)}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }
        return _host.Client.SendAsync(request);
    }

    private static Dictionary<string, string> ClientCredentials(string clientId, string? secret) =>
        secret is null
            ? new() { ["grant_type"] = "client_credentials", ["client_id"] = clientId }
            : new() { ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = secret };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<JsonWebToken> IssueAsync(string slug, string clientId, string secret)
    {
        var response = await PostTokenAsync(slug, ClientCredentials(clientId, secret));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await JsonAsync(response)).GetProperty("access_token").GetString()!;
        return new JsonWebTokenHandler().ReadJsonWebToken(token);
    }

    private async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(error, (await JsonAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Passthrough_IssuesToken_ForTenantClient()
    {
        var response = await PostTokenAsync("acme", ClientCredentials("svc", FakeDirectory.AcmeSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("Bearer", body.GetProperty("token_type").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
    }

    private static TokenValidationParameters ValidationFor(FakeDirectory.TenantEntry tenant)
    {
        var publicKey = System.Security.Cryptography.ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(tenant.Key.ExportSubjectPublicKeyInfo(), out _);
        return new TokenValidationParameters
        {
            ValidIssuer = $"{TokenTestHost.IssuerBase}/tenants/{tenant.Slug}",
            ValidAudience = $"{TokenTestHost.IssuerBase}/tenants/{tenant.Slug}/api",
            IssuerSigningKey = new ECDsaSecurityKey(publicKey) { KeyId = tenant.KeyId.ToString("N") },
            ValidTypes = ["at+jwt"]
        };
    }

    [Fact]
    public async Task AccessToken_HasTenantIssuerAudienceKidAndClaims()
    {
        var jwt = await IssueAsync("acme", "svc", FakeDirectory.AcmeSecret);
        var acme = _host.Directory.Tenant("acme");

        Assert.Equal("ES256", jwt.Alg);
        Assert.Equal("at+jwt", jwt.Typ);
        Assert.Equal(acme.KeyId.ToString("N"), jwt.Kid);
        Assert.Equal("https://wardenid.test/tenants/acme", jwt.Issuer);
        Assert.Equal(new[] { "https://wardenid.test/tenants/acme/api" }, jwt.Audiences);
        Assert.Equal("svc", jwt.Subject);
        Assert.Equal("svc", jwt.GetClaim("client_id").Value);
        Assert.Equal(acme.TenantId.ToString(), jwt.GetClaim(CustomClaimTypes.TenantId).Value);
        Assert.Equal(TokenUse.Access, jwt.GetClaim(CustomClaimTypes.TokenUse).Value);
    }

    [Fact]
    public async Task AccessToken_FromAcme_ValidatesWithAcmeKey_FailsWithGlobexKey()
    {
        var jwt = await IssueAsync("acme", "svc", FakeDirectory.AcmeSecret);
        var handler = new JsonWebTokenHandler();

        var withAcme = await handler.ValidateTokenAsync(jwt.EncodedToken, ValidationFor(_host.Directory.Tenant("acme")));
        var globexParameters = ValidationFor(_host.Directory.Tenant("globex"));
        globexParameters.ValidIssuer = "https://wardenid.test/tenants/acme";
        globexParameters.ValidAudience = "https://wardenid.test/tenants/acme/api";
        var withGlobex = await handler.ValidateTokenAsync(jwt.EncodedToken, globexParameters);

        Assert.True(withAcme.IsValid, withAcme.Exception?.ToString());
        Assert.False(withGlobex.IsValid);
    }

    [Fact]
    public async Task ClientSecretBasic_IsAccepted()
    {
        var response = await PostTokenAsync(
            "acme", new() { ["grant_type"] = "client_credentials" }, ("svc", FakeDirectory.AcmeSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RotatedSecret_IsAccepted() =>
        await IssueAsync("acme", "svc", FakeDirectory.AcmeRotatedSecret);

    [Fact]
    public async Task SameClientIdAtOtherTenant_UsesThatTenantsSecret()
    {
        await AssertErrorAsync(
            await PostTokenAsync("globex", ClientCredentials("svc", FakeDirectory.AcmeSecret)),
            HttpStatusCode.Unauthorized, "invalid_client");
        await IssueAsync("globex", "svc", FakeDirectory.GlobexSecret);
    }

    [Fact]
    public async Task ClientLifetimeOverride_SetsExpiresIn()
    {
        var response = await PostTokenAsync("acme", ClientCredentials("short", FakeDirectory.AcmeSecret));
        var body = await JsonAsync(response);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("access_token").GetString()!);

        // OpenIddict stores dates at second precision and computes expires_in = round(exp - now),
        // so a 60 s token can report 59 depending on the sub-second clock. The token itself is exact.
        Assert.InRange(body.GetProperty("expires_in").GetInt32(), 59, 60);
        Assert.Equal(TimeSpan.FromSeconds(60), jwt.ValidTo - jwt.IssuedAt);
    }

    [Fact]
    public async Task Response_HasNoStore_AndNoRefreshOrIdToken()
    {
        var response = await PostTokenAsync("acme", ClientCredentials("svc", FakeDirectory.AcmeSecret));
        var body = await JsonAsync(response);

        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.False(body.TryGetProperty("refresh_token", out _));
        Assert.False(body.TryGetProperty("id_token", out _));
    }

    [Fact]
    public async Task UnknownTenant_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound,
            (await PostTokenAsync("initech", ClientCredentials("svc", FakeDirectory.AcmeSecret))).StatusCode);

    [Fact]
    public async Task MixedCaseSlug_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound,
            (await PostTokenAsync("ACME", ClientCredentials("svc", FakeDirectory.AcmeSecret))).StatusCode);

    [Fact]
    public async Task RootTokenPath_Returns404()
    {
        var response = await _host.Client.PostAsync("/token",
            new FormUrlEncodedContent(ClientCredentials("svc", FakeDirectory.AcmeSecret)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SuspendedTenant_IsUnauthorizedClient() =>
        await AssertErrorAsync(
            await PostTokenAsync("frozen", ClientCredentials("svc", FakeDirectory.AcmeSecret)),
            HttpStatusCode.BadRequest, "unauthorized_client");

    [Theory]
    [InlineData("nope", FakeDirectory.AcmeSecret)]
    [InlineData("svc", "wrong")]
    [InlineData("disabled", FakeDirectory.AcmeSecret)]
    [InlineData("pkjwt", FakeDirectory.AcmeSecret)]
    public async Task BadClientCredentials_AreInvalidClient(string clientId, string secret) =>
        await AssertErrorAsync(
            await PostTokenAsync("acme", ClientCredentials(clientId, secret)),
            HttpStatusCode.Unauthorized, "invalid_client");

    // OpenIddict rejects client_credentials without a client_secret/client_assertion up front (ID2057).
    [Fact]
    public async Task EmptySecret_IsInvalidRequest() =>
        await AssertErrorAsync(
            await PostTokenAsync("acme", ClientCredentials("svc", "")),
            HttpStatusCode.BadRequest, "invalid_request");

    [Fact]
    public async Task PublicClient_WithoutSecret_IsInvalidRequest() =>
        await AssertErrorAsync(
            await PostTokenAsync("acme", ClientCredentials("public-app", null)),
            HttpStatusCode.BadRequest, "invalid_request");

    [Fact]
    public async Task PublicClient_WithSecret_IsUnauthorizedClient() =>
        await AssertErrorAsync(
            await PostTokenAsync("acme", ClientCredentials("public-app", FakeDirectory.AcmeSecret)),
            HttpStatusCode.BadRequest, "unauthorized_client");

    [Fact]
    public async Task ClientWithoutClientCredentialsGrant_IsUnauthorizedClient() =>
        await AssertErrorAsync(
            await PostTokenAsync("acme", ClientCredentials("no-cc", FakeDirectory.AcmeSecret)),
            HttpStatusCode.BadRequest, "unauthorized_client");

    [Fact]
    public async Task AnyScope_IsInvalidScope()
    {
        var form = ClientCredentials("svc", FakeDirectory.AcmeSecret);
        form["scope"] = "api.read";

        await AssertErrorAsync(await PostTokenAsync("acme", form), HttpStatusCode.BadRequest, "invalid_scope");
    }

    [Fact]
    public async Task EmptyScopeParameter_IsTreatedAsNoScope()
    {
        var form = ClientCredentials("svc", FakeDirectory.AcmeSecret);
        form["scope"] = "";

        Assert.Equal(HttpStatusCode.OK, (await PostTokenAsync("acme", form)).StatusCode);
    }

    [Fact]
    public async Task OtherGrantType_IsUnsupportedGrantType()
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["username"] = "u", ["password"] = "p",
            ["client_id"] = "svc", ["client_secret"] = FakeDirectory.AcmeSecret
        };

        await AssertErrorAsync(await PostTokenAsync("acme", form), HttpStatusCode.BadRequest, "unsupported_grant_type");
    }

    [Fact]
    public async Task DualClientCredentials_IsInvalidRequest() =>
        await AssertErrorAsync(
            await PostTokenAsync("acme", ClientCredentials("svc", FakeDirectory.AcmeSecret), ("svc", FakeDirectory.AcmeSecret)),
            HttpStatusCode.BadRequest, "invalid_request");

    [Fact]
    public async Task ParallelRequests_ForTwoTenants_EachGetOwnIssuer()
    {
        var tasks = Enumerable.Range(0, 20).Select(i => i % 2 == 0
            ? IssueAsync("acme", "svc", FakeDirectory.AcmeSecret)
            : IssueAsync("globex", "svc", FakeDirectory.GlobexSecret)).ToArray();

        var tokens = await Task.WhenAll(tasks);

        for (var i = 0; i < tokens.Length; i++)
        {
            var slug = i % 2 == 0 ? "acme" : "globex";
            Assert.Equal($"https://wardenid.test/tenants/{slug}", tokens[i].Issuer);
            Assert.Equal(_host.Directory.Tenant(slug).KeyId.ToString("N"), tokens[i].Kid);
        }
    }

    [Fact]
    public async Task PlaceholderServerKeys_NeverSignAccessTokens()
    {
        var jwt = await IssueAsync("acme", "svc", FakeDirectory.AcmeSecret);
        var serverOptions = _host.App.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        var placeholderKids = serverOptions.SigningCredentials.Select(c => c.Key.KeyId).ToHashSet();

        Assert.DoesNotContain(jwt.Kid, placeholderKids);
        Assert.Equal(_host.Directory.Tenant("acme").KeyId.ToString("N"), jwt.Kid);
    }

    [Fact]
    public async Task Discovery_AdvertisesExactlyWhatOpenIddictEnables()
    {
        var discovery = await JsonAsync(await _host.Client.GetAsync("/tenants/acme/.well-known/openid-configuration"));
        var serverOptions = _host.App.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;

        var advertisedGrants = discovery.GetProperty("grant_types_supported").EnumerateArray().Select(e => e.GetString()!).ToHashSet();
        var advertisedAuth = discovery.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(e => e.GetString()!).ToHashSet();

        Assert.True(advertisedGrants.SetEquals(serverOptions.GrantTypes), string.Join(",", serverOptions.GrantTypes));
        Assert.True(advertisedAuth.SetEquals(serverOptions.ClientAuthenticationMethods), string.Join(",", serverOptions.ClientAuthenticationMethods));
        Assert.Equal("https://wardenid.test/tenants/acme/token", discovery.GetProperty("token_endpoint").GetString());
    }

    [Fact]
    public async Task Discovery_ReturnsWardenIdDocument_NotOpenIddicts()
    {
        var response = await _host.Client.GetAsync("/tenants/acme/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await JsonAsync(response);

        Assert.Equal(new[] { "ES256" }, doc.GetProperty("id_token_signing_alg_values_supported").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal("https://wardenid.test/tenants/acme/jwks", doc.GetProperty("jwks_uri").GetString());
        Assert.Equal("https://wardenid.test/tenants/acme", doc.GetProperty("issuer").GetString());
        Assert.False(doc.TryGetProperty("prompt_values_supported", out _));
    }

    [Fact]
    public async Task OpenIddictJwksEndpoint_IsNotExposed()
    {
        var response = await _host.Client.GetAsync("/tenants/acme/.well-known/jwks");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenIddictOAuthMetadataEndpoint_IsNotExposed()
    {
        var response = await _host.Client.GetAsync("/tenants/acme/.well-known/oauth-authorization-server");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Discovery_ForSuspendedTenant_Returns200()
    {
        var response = await _host.Client.GetAsync("/tenants/frozen/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
