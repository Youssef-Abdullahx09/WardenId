using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Domain;
using WardenId.Protocol.Infrastructure.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static WardenId.Protocol.Presentation.Tests.TokenFlow;
using RevokeClientGrantsCommand = WardenId.Protocol.Application.Features.Grants.RevokeForClient.Command;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>W12: revoke everything a client holds in a tenant, against the real OpenIddict pipeline.</summary>
public class ClientGrantFlowTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Guid Acme => _host.Directory.Tenant("acme").TenantId;

    private Task<FluentResults.Result> RevokeAsync(string slug, string clientId) =>
        _host.SendAsync(new RevokeClientGrantsCommand(
            _host.Directory.Tenant(slug).TenantId, _host.Directory.Client(slug, clientId).Id));

    private async Task<bool> IsActiveAsync(string token, string clientId, string secret = FakeDirectory.AcmeSecret, string slug = "acme") =>
        (await JsonAsync(await IntrospectAsync(_host, token, clientId, secret, slug))).GetProperty("active").GetBoolean();

    [Fact]
    public async Task RevokesEveryGrantOfTheClient_AndLeavesOtherClients()
    {
        var first = await CodeTokensAsync(_host);
        var second = await CodeTokensAsync(_host);
        var other = await CodeTokensAsync(_host, clientId: "web-rt-other");

        var result = await RevokeAsync("acme", "web-rt");

        Assert.True(result.IsSuccess);
        foreach (var tokens in new[] { first, second })
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                (await RefreshAsync(_host, tokens.GetProperty("refresh_token").GetString()!)).StatusCode);
            Assert.False(await IsActiveAsync(tokens.GetProperty("access_token").GetString()!, "web-rt"));
        }
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(
            _host, other.GetProperty("refresh_token").GetString()!, clientId: "web-rt-other")).StatusCode);
    }

    [Fact]
    public async Task ClientCredentialsToken_IsStampedWithTenantAndClient()
    {
        var token = (await ClientCredentialsAsync(_host)).GetProperty("access_token").GetString()!;

        using var scope = _host.App.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>()
            .Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().SingleAsync();
        var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.Properties!)!;
        Assert.Equal(new GrantOwner(Acme, "svc"), AuthorizationOwnerProperties.Read(properties));
        Assert.True(await IsActiveAsync(token, "svc"));
    }

    // client_id "svc" exists in both tenants: only the routed tenant's tokens die.
    [Fact]
    public async Task RevokesTheClientsClientCredentialsTokens_OnlyInItsTenant()
    {
        var acme = (await ClientCredentialsAsync(_host)).GetProperty("access_token").GetString()!;
        var globex = (await ClientCredentialsAsync(_host, slug: "globex", secret: FakeDirectory.GlobexSecret))
            .GetProperty("access_token").GetString()!;

        var result = await RevokeAsync("acme", "svc");

        Assert.True(result.IsSuccess);
        Assert.False(await IsActiveAsync(acme, "svc"));
        Assert.True(await IsActiveAsync(globex, "svc", FakeDirectory.GlobexSecret, "globex"));
    }

    [Fact]
    public async Task ClientOfAnotherTenant_IsNotFound()
    {
        var globexSvc = _host.Directory.Client("globex", "svc").Id;

        var result = await _host.SendAsync(new RevokeClientGrantsCommand(Acme, globexSvc));

        Assert.Equal(ProtocolErrorCodes.NotFound, Assert.Single(result.Errors).Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task NothingToRevoke_Succeeds() =>
        Assert.True((await RevokeAsync("acme", "web")).IsSuccess);

    // Documented limit: an authorization written before stamping names no tenant, so it can't be proven to be this one's.
    [Fact]
    public async Task UnstampedAuthorization_IsNotFound_AndStaysValid()
    {
        string id;
        using (var scope = _host.App.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var created = await manager.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                Subject = _host.Directory.User("alice").UserId.ToString(),
                Status = Statuses.Valid,
                Type = AuthorizationTypes.AdHoc
            });
            id = (await manager.GetIdAsync(created))!;
        }

        Assert.True((await RevokeAsync("acme", "web-rt")).IsSuccess);

        using var check = _host.App.Services.CreateScope();
        var authorizations = check.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        Assert.Equal(Statuses.Valid, await authorizations.GetStatusAsync((await authorizations.FindByIdAsync(id))!));
    }
}
