using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Time.Testing;
using static WardenId.Protocol.Presentation.Tests.TokenFlow;

namespace WardenId.Protocol.Presentation.Tests;

public class AccessTokenAuthenticationTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static Task<HttpResponseMessage> ProbeAsync(ProtocolTestHost host, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ProtocolTestHost.AccessTokenProbePath);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return host.Client.SendAsync(request);
    }

    private static void AssertUnauthorized(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task UserAccessToken_Authenticates()
    {
        var alice = _host.Directory.User("alice");
        var access = (await CodeTokensAsync(_host)).GetProperty("access_token").GetString()!;

        var response = await ProbeAsync(_host, access);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(alice.UserId.ToString(), body.GetProperty("sub").GetString());
        Assert.Equal(_host.Directory.Tenant("acme").TenantId.ToString(), body.GetProperty("tenant_id").GetString());
    }

    [Fact]
    public async Task ClientCredentialsAccessToken_Authenticates()
    {
        var access = (await ClientCredentialsAsync(_host)).GetProperty("access_token").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await ProbeAsync(_host, access)).StatusCode);
    }

    [Fact]
    public async Task NoToken_IsChallenged() => AssertUnauthorized(await ProbeAsync(_host, null));

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("abc.def.ghi")]
    [InlineData("eyJhbGciOiJFUzI1NiJ9.eyJpc3MiOiJodHRwczovL2V2aWwudGVzdCJ9.c2ln")]
    public async Task Garbage_IsRejected(string token) => AssertUnauthorized(await ProbeAsync(_host, token));

    [Fact]
    public async Task IdentityToken_IsRejected()
    {
        var idToken = (await CodeTokensAsync(_host)).GetProperty("id_token").GetString()!;

        AssertUnauthorized(await ProbeAsync(_host, idToken));
    }

    [Fact]
    public async Task TamperedSignature_IsRejected()
    {
        var access = (await CodeTokensAsync(_host)).GetProperty("access_token").GetString()!;
        var parts = access.Split('.');
        var signature = parts[2].ToCharArray();
        signature[5] = signature[5] == 'A' ? 'B' : 'A';

        AssertUnauthorized(await ProbeAsync(_host, $"{parts[0]}.{parts[1]}.{new string(signature)}"));
    }

    [Fact]
    public async Task RefreshToken_IsRejected()
    {
        var refresh = (await CodeTokensAsync(_host)).GetProperty("refresh_token").GetString()!;

        AssertUnauthorized(await ProbeAsync(_host, refresh));
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        // Issued two hours ago by the host's clock; access tokens live far shorter.
        var past = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-2));
        await using var host = await ProtocolTestHost.StartAsync(past);
        var access = (await ClientCredentialsAsync(host)).GetProperty("access_token").GetString()!;

        AssertUnauthorized(await ProbeAsync(host, access));
    }
}
