using System.Net;
using static WardenId.Protocol.Presentation.Tests.TokenFlow;

namespace WardenId.Protocol.Presentation.Tests;

public class RevocationFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<bool> IsActiveAsync(string token, string clientId = "web-rt") =>
        (await JsonAsync(await IntrospectAsync(_host, token, clientId))).GetProperty("active").GetBoolean();

    private static async Task AssertOkEmptyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync())
                    || await response.Content.ReadAsStringAsync() == "{}");
    }

    [Fact]
    public async Task RevokeRefreshToken_RevokesWholeFamily()
    {
        var tokens = await CodeTokensAsync(_host);
        var access = tokens.GetProperty("access_token").GetString()!;
        var refresh = tokens.GetProperty("refresh_token").GetString()!;

        await AssertOkEmptyAsync(await RevokeAsync(_host, refresh, hint: "refresh_token"));

        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(_host, refresh)).StatusCode);
        Assert.False(await IsActiveAsync(access));
    }

    [Fact]
    public async Task RevokeAccessToken_RevokesOnlyThatToken()
    {
        var tokens = await CodeTokensAsync(_host);
        var access = tokens.GetProperty("access_token").GetString()!;

        await AssertOkEmptyAsync(await RevokeAsync(_host, access, hint: "access_token"));

        Assert.False(await IsActiveAsync(access));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_host, tokens.GetProperty("refresh_token").GetString()!)).StatusCode);
    }

    // Review focus 4.
    [Fact]
    public async Task RevokeTwice_IsOk()
    {
        var refresh = (await CodeTokensAsync(_host)).GetProperty("refresh_token").GetString()!;

        await AssertOkEmptyAsync(await RevokeAsync(_host, refresh));
        await AssertOkEmptyAsync(await RevokeAsync(_host, refresh));
    }

    [Fact]
    public async Task UnknownToken_IsOk() =>
        await AssertOkEmptyAsync(await RevokeAsync(_host, "not-a-token"));

    // Review focus 1.
    [Fact]
    public async Task OtherTenantsTokens_AreUntouched()
    {
        var tokens = await CodeTokensAsync(_host);
        var refresh = tokens.GetProperty("refresh_token").GetString()!;
        var access = tokens.GetProperty("access_token").GetString()!;

        await AssertOkEmptyAsync(await RevokeAsync(_host, refresh, secret: FakeDirectory.GlobexSecret, slug: "globex"));
        await AssertOkEmptyAsync(await RevokeAsync(_host, access, secret: FakeDirectory.GlobexSecret, slug: "globex"));

        Assert.True(await IsActiveAsync(access));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_host, refresh)).StatusCode);
    }

    [Fact]
    public async Task OtherClientsToken_IsUntouched()
    {
        var refresh = (await CodeTokensAsync(_host)).GetProperty("refresh_token").GetString()!;

        await AssertOkEmptyAsync(await RevokeAsync(_host, refresh, clientId: "web-rt-other"));

        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_host, refresh)).StatusCode);
    }

    [Fact]
    public async Task PublicClient_RevokesOwnRefreshToken_WithClientIdOnly()
    {
        var refresh = (await CodeTokensAsync(_host, "spa-rt", secret: null, redirectUri: FakeDirectory.SpaRtRedirect))
            .GetProperty("refresh_token").GetString()!;

        await AssertOkEmptyAsync(await RevokeAsync(_host, refresh, clientId: "spa-rt", secret: null));

        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(_host, refresh, clientId: "spa-rt", secret: null)).StatusCode);
    }

    [Fact]
    public async Task BadSecret_IsInvalidClient()
    {
        var refresh = (await CodeTokensAsync(_host)).GetProperty("refresh_token").GetString()!;

        var response = await RevokeAsync(_host, refresh, secret: "wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
