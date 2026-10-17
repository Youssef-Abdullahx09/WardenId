using WardenId.ClientManagement.Application.Features.Clients.GetReference;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.GetReference;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsThePublicClientId_ForTheGuid()
    {
        var client = ClientBuilder.Confidential(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ClientReference(client.Id, client.ClientId.Value), result.Value);
    }

    [Fact]
    public async Task Handle_FindsADisabledClient()
    {
        var client = ClientBuilder.Confidential(TenantId);
        client.Disable();
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForOtherTenant()
    {
        var client = ClientBuilder.Confidential(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(Guid.NewGuid(), client.Id), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForSoftDeletedClient()
    {
        var client = ClientBuilder.Confidential(TenantId);
        client.MarkAsDeleted(Guid.Empty);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(db).Handle(new Query(TenantId, client.Id), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownId()
    {
        await using var db = await ClientBuilder.SeedAsync(ClientBuilder.Confidential(TenantId));

        var result = await new Handler(db).Handle(new Query(TenantId, Guid.NewGuid()), CancellationToken.None);

        AssertNotFound(result);
    }

    private static void AssertNotFound(Result<ClientReference> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.NotFound, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
