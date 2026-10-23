using System.Net;
using System.Net.Http.Headers;
using static WardenId.Protocol.Presentation.Tests.TokenFlow;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Presentation.Tests;

public class UserInfoFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<HttpResponseMessage> GetAsync(string? token, string slug = "acme")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/tenants/{slug}/userinfo");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _host.Client.SendAsync(request);
    }

    private static void AssertBearerError(HttpResponseMessage response, HttpStatusCode status, string? error)
    {
        Assert.Equal(status, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        if (error is not null) Assert.Contains($"error=\"{error}\"", challenge.Parameter);
    }

    [Fact]
    public async Task ProfileAndEmail_ReturnsClaims()
    {
        var alice = _host.Directory.User("alice");
        var access = (await CodeTokensAsync(_host, scope: "openid profile email")).GetProperty("access_token").GetString()!;

        var response = await GetAsync(access);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(alice.UserId.ToString(), body.GetProperty("sub").GetString());
        Assert.Equal("alice", body.GetProperty("preferred_username").GetString());
        Assert.Equal("Alice", body.GetProperty("given_name").GetString());
        Assert.Equal("Liddell", body.GetProperty("family_name").GetString());
        Assert.Equal("Alice Liddell", body.GetProperty("name").GetString());
        Assert.Equal(alice.Email, body.GetProperty("email").GetString());
        Assert.True(body.GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    public async Task OpenIdOnly_ReturnsSubOnly()
    {
        var access = (await CodeTokensAsync(_host, scope: "openid")).GetProperty("access_token").GetString()!;

        var body = await JsonAsync(await GetAsync(access));

        Assert.Equal(new[] { "sub" }, body.EnumerateObject().Select(p => p.Name).ToArray());
    }

    // Review focus 3: RFC 6750 §2.2 form-encoded body parameter.
    [Fact]
    public async Task Post_WithFormToken_Works()
    {
        var access = (await CodeTokensAsync(_host, scope: "openid email")).GetProperty("access_token").GetString()!;

        var response = await _host.Client.PostAsync("/tenants/acme/userinfo",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = access }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await JsonAsync(response)).TryGetProperty("email", out _));
    }

    [Fact]
    public async Task ClientCredentialsToken_IsInsufficientScope()
    {
        var access = (await ClientCredentialsAsync(_host)).GetProperty("access_token").GetString()!;

        AssertBearerError(await GetAsync(access), HttpStatusCode.Forbidden, "insufficient_scope");
    }

    [Fact]
    public async Task NoToken_Is401() =>
        AssertBearerError(await GetAsync(null), HttpStatusCode.Unauthorized, error: null);

    [Fact]
    public async Task GarbageToken_IsInvalidToken() =>
        AssertBearerError(await GetAsync("not-a-token"), HttpStatusCode.Unauthorized, "invalid_token");

    [Fact]
    public async Task TokenFromOtherTenant_IsInvalidToken()
    {
        var access = (await CodeTokensAsync(_host)).GetProperty("access_token").GetString()!;

        AssertBearerError(await GetAsync(access, slug: "globex"), HttpStatusCode.Unauthorized, "invalid_token");
    }

    [Fact]
    public async Task UserDisabledAfterIssue_IsInvalidToken()
    {
        var access = (await CodeTokensAsync(_host)).GetProperty("access_token").GetString()!;
        _host.Directory.User("alice").Status = UserStatus.Disabled;

        AssertBearerError(await GetAsync(access), HttpStatusCode.Unauthorized, "invalid_token");
    }

    [Fact]
    public async Task RevokedAccessToken_IsInvalidToken()
    {
        var access = (await CodeTokensAsync(_host)).GetProperty("access_token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await RevokeAsync(_host, access)).StatusCode);

        AssertBearerError(await GetAsync(access), HttpStatusCode.Unauthorized, "invalid_token");
    }

    // Review focus 5.
    [Fact]
    public async Task IdTokenOrRefreshToken_AsBearer_Is401()
    {
        var tokens = await CodeTokensAsync(_host);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(tokens.GetProperty("id_token").GetString()!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(tokens.GetProperty("refresh_token").GetString()!)).StatusCode);
    }
}
