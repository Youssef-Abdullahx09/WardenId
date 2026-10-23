using WardenId.ClientManagement.Application.Features.Clients.ListCorsOrigins;
using WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.ListCorsOrigins.Query;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.ListCorsOrigins;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins.Query;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsTheDistinctOrigins_InOrdinalOrder()
    {
        var clientsQuery = new FakeClientsQuery
        {
            ListCorsOriginsResponse = Result.Ok(new List<string>
            {
                "https://spa.example", "https://admin.example", "https://Zeta.example", "https://spa.example"
            })
        };

        var result = await new Handler(clientsQuery).Handle(new Query(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "https://Zeta.example", "https://admin.example", "https://spa.example" }, result.Value);
        Assert.Equal(new ClientsQuery(TenantId), Assert.Single(clientsQuery.ListCorsOriginsCalls));
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_ForATenantWithoutOrigins()
    {
        var result = await new Handler(new FakeClientsQuery()).Handle(new Query(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
