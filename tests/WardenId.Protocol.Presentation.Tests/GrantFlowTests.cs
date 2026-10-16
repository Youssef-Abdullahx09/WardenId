using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Domain;
using WardenId.Protocol.Infrastructure.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;
using ListGrantsQuery = WardenId.Protocol.Application.Features.Grants.ListForUser.Query;
using RevokeGrantCommand = WardenId.Protocol.Application.Features.Grants.Revoke.Command;
using RevokeUserGrantsCommand = WardenId.Protocol.Application.Features.Grants.RevokeForUser.Command;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>W11 admin grants against the real OpenIddict pipeline: stamping at /authorize, listing, revoking.</summary>
public class GrantFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Guid Acme => _host.Directory.Tenant("acme").TenantId;
    private Guid Globex => _host.Directory.Tenant("globex").TenantId;
    private Guid Alice => _host.Directory.User("alice").UserId;

    private async Task<IReadOnlyList<UserGrant>> GrantsAsync(Guid tenantId, Guid userId)
    {
        var result = await _host.SendAsync(new ListGrantsQuery(tenantId, userId));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task AssertRefreshRejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Errors.InvalidGrant, (await TokenFlow.JsonAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Authorize_StampsTheAuthorizationWithTenantAndClient()
    {
        await TokenFlow.CodeTokensAsync(_host);

        using var scope = _host.App.Services.CreateScope();
        var authorization = await scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>()
            .Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().SingleAsync();
        var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(authorization.Properties!)!;
        Assert.Equal(new GrantOwner(Acme, "web-rt"), AuthorizationOwnerProperties.Read(properties));
    }

    [Fact]
    public async Task List_ReturnsTheUsersGrants_WithClientScopesAndOfflineAccess()
    {
        var browser = _host.CreateBrowser();
        await TokenFlow.CodeTokensAsync(_host, browser: browser);
        await TokenFlow.CodeTokensAsync(_host, clientId: "web", scope: "openid profile", browser: browser);

        var grants = await GrantsAsync(Acme, Alice);

        Assert.Equal(2, grants.Count);
        var offline = grants.Single(g => g.ClientId == "web-rt");
        Assert.True(offline.OfflineAccess);
        Assert.Contains(Scopes.OfflineAccess, offline.Scopes);
        Assert.Equal(GrantStatus.Valid, offline.Status);
        Assert.NotNull(offline.CreatedAt);
        var online = grants.Single(g => g.ClientId == "web");
        Assert.False(online.OfflineAccess);
        Assert.Equal(new[] { Scopes.OpenId, Scopes.Profile }, online.Scopes.Order());
    }

    // alice's subject is a globally unique user id, yet another tenant in the route must still see nothing.
    [Fact]
    public async Task OtherTenantInRoute_SeesNoGrant_AndCannotRevokeOne()
    {
        await TokenFlow.CodeTokensAsync(_host);
        var grant = Assert.Single(await GrantsAsync(Acme, Alice));

        Assert.Empty(await GrantsAsync(Globex, Alice));
        var revoke = await _host.SendAsync(new RevokeGrantCommand(Globex, Alice, grant.AuthorizationId));

        Assert.Equal(ProtocolErrorCodes.NotFound, revoke.Errors.Single().Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Equal(GrantStatus.Valid, Assert.Single(await GrantsAsync(Acme, Alice)).Status);
    }

    [Fact]
    public async Task OtherUsersGrant_IsNotFound()
    {
        await TokenFlow.CodeTokensAsync(_host);
        var grant = Assert.Single(await GrantsAsync(Acme, Alice));

        var revoke = await _host.SendAsync(
            new RevokeGrantCommand(Acme, _host.Directory.User("dave").UserId, grant.AuthorizationId));

        Assert.True(revoke.IsFailed);
        Assert.Equal(GrantStatus.Valid, Assert.Single(await GrantsAsync(Acme, Alice)).Status);
    }

    [Fact]
    public async Task Revoke_KillsTheRefreshTokenFamily_AndLeavesOtherGrants()
    {
        var browser = _host.CreateBrowser();
        var revokedTokens = await TokenFlow.CodeTokensAsync(_host, browser: browser);
        var keptTokens = await TokenFlow.CodeTokensAsync(_host, clientId: "web-rt-other", browser: browser);
        var grant = (await GrantsAsync(Acme, Alice)).Single(g => g.ClientId == "web-rt");

        var result = await _host.SendAsync(new RevokeGrantCommand(Acme, Alice, grant.AuthorizationId));

        Assert.True(result.IsSuccess);
        await AssertRefreshRejectedAsync(
            await TokenFlow.RefreshAsync(_host, revokedTokens.GetProperty("refresh_token").GetString()!));
        var introspection = await TokenFlow.JsonAsync(
            await TokenFlow.IntrospectAsync(_host, revokedTokens.GetProperty("access_token").GetString()!));
        Assert.False(introspection.GetProperty("active").GetBoolean());
        var kept = await TokenFlow.RefreshAsync(
            _host, keptTokens.GetProperty("refresh_token").GetString()!, clientId: "web-rt-other");
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);

        var grants = await GrantsAsync(Acme, Alice);
        Assert.Equal(GrantStatus.Revoked, grants.Single(g => g.ClientId == "web-rt").Status);
        Assert.Equal(GrantStatus.Valid, grants.Single(g => g.ClientId == "web-rt-other").Status);
    }

    [Fact]
    public async Task RevokeForUser_RevokesEveryGrant_IncludingOffline()
    {
        var browser = _host.CreateBrowser();
        var first = await TokenFlow.CodeTokensAsync(_host, browser: browser);
        var second = await TokenFlow.CodeTokensAsync(_host, clientId: "web-rt-other", browser: browser);

        var result = await _host.SendAsync(new RevokeUserGrantsCommand(Acme, Alice));

        Assert.True(result.IsSuccess);
        Assert.All(await GrantsAsync(Acme, Alice), g => Assert.Equal(GrantStatus.Revoked, g.Status));
        await AssertRefreshRejectedAsync(
            await TokenFlow.RefreshAsync(_host, first.GetProperty("refresh_token").GetString()!));
        await AssertRefreshRejectedAsync(await TokenFlow.RefreshAsync(
            _host, second.GetProperty("refresh_token").GetString()!, clientId: "web-rt-other"));
    }
}
