using WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;
using WardenId.Protocol.Application.Features.Grants.RevokeForClient;
using GetReferenceQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference.Query;

namespace WardenId.Protocol.Application.Tests.Features.Grants.RevokeForClient;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ClientGuid = Guid.NewGuid();

    [Fact]
    public async Task ResolvesTheClientsPublicId_AndRevokesItsGrantsInTheTenant()
    {
        var sender = new StubSender().Returns<GetReferenceQuery, Result<ClientReference>>(
            Result.Ok(new ClientReference(ClientGuid, "web")));
        var revoker = new FakeClientGrantRevoker();

        var result = await new Handler(sender, revoker).Handle(new Command(TenantId, ClientGuid), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new GetReferenceQuery(TenantId, ClientGuid), Assert.Single(sender.Sent));
        Assert.Equal((TenantId, "web"), Assert.Single(revoker.Calls));
    }

    [Fact]
    public async Task UnknownClient_IsNotFound_AndRevokesNothing()
    {
        var sender = new StubSender().Returns<GetReferenceQuery, Result<ClientReference>>(
            Result.Fail<ClientReference>("Client not found."));
        var revoker = new FakeClientGrantRevoker();

        var result = await new Handler(sender, revoker).Handle(new Command(TenantId, ClientGuid), CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotFound, Assert.Single(result.Errors).Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Empty(revoker.Calls);
    }
}
