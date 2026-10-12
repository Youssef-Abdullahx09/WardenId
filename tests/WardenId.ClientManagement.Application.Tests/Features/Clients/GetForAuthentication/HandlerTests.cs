using WardenId.ClientManagement.Application.Features.Clients.GetForAuthentication;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.GetForAuthentication;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsClient_WithMappedFields()
    {
        var client = ClientBuilder.Confidential(TenantId, GrantType.ClientCredentials | GrantType.RefreshToken);
        client.UpdateTokenLifetimeOverrides(TokenLifetimeOverrides.Create(600, null, null).Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.ClientId.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var value = result.Value;
        Assert.Equal(client.Id, value.Id);
        Assert.Equal(client.ClientId.Value, value.ClientId);
        Assert.Equal(ClientKind.Confidential, value.Kind);
        Assert.True(value.Enabled);
        Assert.Equal(new[] { AllowedGrant.ClientCredentials, AllowedGrant.RefreshToken }, value.AllowedGrants);
        Assert.Equal(ClientAuthMethod.SharedSecret, value.AuthenticationMethod);
        Assert.Equal(600, value.AccessTokenLifetimeSeconds);
    }

    [Fact]
    public async Task Handle_ReturnsDisabledClient_WithEnabledFalse()
    {
        var client = ClientBuilder.Confidential(TenantId);
        client.Disable();
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.ClientId.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Enabled);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForOtherTenant()
    {
        var client = ClientBuilder.Confidential(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(Guid.NewGuid(), client.ClientId.Value), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownClientId()
    {
        await using var db = await ClientBuilder.SeedAsync(ClientBuilder.Confidential(TenantId));

        var result = await new Handler(db).Handle(new Query(TenantId, "nope"), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForEmptyClientId()
    {
        await using var db = await ClientBuilder.SeedAsync(ClientBuilder.Confidential(TenantId));

        var result = await new Handler(db).Handle(new Query(TenantId, ""), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForSoftDeletedClient()
    {
        var client = ClientBuilder.Confidential(TenantId);
        client.MarkAsDeleted(Guid.Empty);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.ClientId.Value), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_MapsRedirectUris_AndRequirePkce_ForPublicClient()
    {
        var client = ClientBuilder.Public(TenantId);
        client.AddRedirectUri(RedirectUri.Create("https://spa.example/cb").Value);
        client.AddRedirectUri(RedirectUri.Create("https://spa.example/silent").Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.ClientId.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "https://spa.example/cb", "https://spa.example/silent" },
            result.Value.RedirectUris.OrderBy(u => u, StringComparer.Ordinal));
        Assert.True(result.Value.RequirePkce);
    }

    [Fact]
    public async Task Handle_ConfidentialClient_DoesNotRequirePkce_AndHasNoRedirectUris()
    {
        var client = ClientBuilder.Confidential(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.ClientId.Value), CancellationToken.None);

        Assert.False(result.Value.RequirePkce);
        Assert.Empty(result.Value.RedirectUris);
    }

    private static void AssertNotFound(Result<ClientForAuthentication> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.NotFound, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
