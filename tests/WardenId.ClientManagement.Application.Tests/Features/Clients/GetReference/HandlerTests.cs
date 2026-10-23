using WardenId.ClientManagement.Application.Features.Clients.GetReference;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetReference.Query;
using QueryItem = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetReference.Item;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.GetReference;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference.Query;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsThePublicClientId_ForTheGuid()
    {
        var id = Guid.NewGuid();
        var clientsQuery = new FakeClientsQuery
        {
            GetReferenceResponse = Result.Ok<QueryItem?>(new QueryItem { Id = id, ClientId = "client-abc" })
        };

        var result = await new Handler(clientsQuery).Handle(new Query(TenantId, id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ClientReference(id, "client-abc"), result.Value);
        Assert.Equal(new ClientsQuery(TenantId, id), Assert.Single(clientsQuery.GetReferenceCalls));
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheQueryFindsNoClient()
    {
        var result = await new Handler(new FakeClientsQuery())
            .Handle(new Query(TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.NotFound, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
