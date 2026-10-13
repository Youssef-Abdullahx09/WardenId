using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using static WardenId.Protocol.Presentation.Tests.TokenFlow;

namespace WardenId.Protocol.Presentation.Tests;

public class IntrospectionFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static async Task AssertInactiveAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.False(body.GetProperty("active").GetBoolean());
        // RFC 7662 §2.2: nothing about why.
        Assert.Single(body.EnumerateObject());
    }

    [Fact]
    public async Task OwnAccessToken_IsActive_WithMetadata()
    {
        var tokens = await CodeTokensAsync(_host);
        var alice = _host.Directory.User("alice");

        var response = await IntrospectAsync(_host, tokens.GetProperty("access_token").GetString()!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.True(body.GetProperty("active").GetBoolean());
        Assert.Equal(alice.UserId.ToString(), body.GetProperty("sub").GetString());
        Assert.Equal("web-rt", body.GetProperty("client_id").GetString());
        Assert.Equal("https://wardenid.test/tenants/acme", body.GetProperty("iss").GetString());
        Assert.Equal(JsonValueKind.Number, body.GetProperty("exp").ValueKind);
        // Presenter callers get metadata only; scope/custom claims need the caller in the audience (FR-CM-04).
        Assert.False(body.TryGetProperty("tenant_id", out _));
    }

    [Fact]
    public async Task OwnRefreshToken_IsActive()
    {
        var tokens = await CodeTokensAsync(_host);

        var body = await JsonAsync(await IntrospectAsync(_host, tokens.GetProperty("refresh_token").GetString()!));

        Assert.True(body.GetProperty("active").GetBoolean());
    }

    // Review focus 2: hints are optional; a wrong one must not change the answer.
    [Fact]
    public async Task WrongTokenTypeHint_StillActive()
    {
        var tokens = await CodeTokensAsync(_host);

        var body = await JsonAsync(await IntrospectAsync(_host, tokens.GetProperty("access_token").GetString()!, hint: "refresh_token"));

        Assert.True(body.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task OtherClientsToken_IsInactive()
    {
        var tokens = await CodeTokensAsync(_host);

        await AssertInactiveAsync(await IntrospectAsync(_host, tokens.GetProperty("access_token").GetString()!, clientId: "web-rt-other"));
    }

    [Fact]
    public async Task AccessTokenFromOtherTenant_IsInactive()
    {
        var tokens = await CodeTokensAsync(_host);

        await AssertInactiveAsync(await IntrospectAsync(
            _host, tokens.GetProperty("access_token").GetString()!, secret: FakeDirectory.GlobexSecret, slug: "globex"));
    }

    // Review focus 1: refresh tokens are Data Protection tokens — only ValidateTokenTenant stops this.
    [Fact]
    public async Task RefreshTokenFromOtherTenant_IsInactive()
    {
        var tokens = await CodeTokensAsync(_host);

        await AssertInactiveAsync(await IntrospectAsync(
            _host, tokens.GetProperty("refresh_token").GetString()!, secret: FakeDirectory.GlobexSecret, slug: "globex"));
    }

    [Fact]
    public async Task ClientCredentialsToken_IsActive_ForItsClient()
    {
        var tokens = await ClientCredentialsAsync(_host);

        var body = await JsonAsync(await IntrospectAsync(_host, tokens.GetProperty("access_token").GetString()!, clientId: "svc"));

        Assert.True(body.GetProperty("active").GetBoolean());
        Assert.Equal("svc", body.GetProperty("client_id").GetString());
    }

    [Fact]
    public async Task GarbageToken_IsInactive() =>
        await AssertInactiveAsync(await IntrospectAsync(_host, "not-a-token"));

    [Fact]
    public async Task ExpiredAccessToken_IsInactive()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await ProtocolTestHost.StartAsync(clock);
        var tokens = await CodeTokensAsync(host);

        clock.Advance(TimeSpan.FromMinutes(16));

        await AssertInactiveAsync(await IntrospectAsync(host, tokens.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task PublicClient_IsUnauthorizedClient()
    {
        var tokens = await CodeTokensAsync(_host, "spa-rt", secret: null, redirectUri: FakeDirectory.SpaRtRedirect);

        var response = await IntrospectAsync(_host, tokens.GetProperty("access_token").GetString()!, clientId: "spa-rt", secret: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unauthorized_client", (await JsonAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task BadSecret_IsInvalidClient()
    {
        var tokens = await CodeTokensAsync(_host);

        var response = await IntrospectAsync(_host, tokens.GetProperty("access_token").GetString()!, secret: "wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", (await JsonAsync(response)).GetProperty("error").GetString());
    }
}
