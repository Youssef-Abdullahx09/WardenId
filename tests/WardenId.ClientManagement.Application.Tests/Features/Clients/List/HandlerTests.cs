using WardenId.ClientManagement.Application.Features.Clients.List;
using WardenId.SharedKernel.Query.Contracts;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.List.Query;
using ListQuery = WardenId.ClientManagement.Application.Features.Clients.List.Query;
using QueryItem = WardenId.ClientManagement.Query.Contracts.Features.Clients.List.Item;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.List;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_PassesFiltersAndPage_ToTheQueryLayer_WithClientTypeByName()
    {
        var clientsQuery = new FakeClientsQuery();

        await new Handler(clientsQuery).Handle(
            new ListQuery(TenantId, 2, 25, Enabled: true, ClientType.Public), CancellationToken.None);

        Assert.Equal(
            new ClientsQuery(TenantId, true, nameof(ClientType.Public), PageNumber: 2, PageSize: 25),
            Assert.Single(clientsQuery.ListCalls));
    }

    [Fact]
    public async Task Handle_PassesNullFilters_AsAny()
    {
        var clientsQuery = new FakeClientsQuery();

        await new Handler(clientsQuery).Handle(new ListQuery(TenantId, 1, 10, null, null), CancellationToken.None);

        Assert.Equal(new ClientsQuery(TenantId, null, null, 1, 10), Assert.Single(clientsQuery.ListCalls));
    }

    [Fact]
    public async Task Handle_MapsRows_InQueryOrder_WithTotal()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var clientsQuery = new FakeClientsQuery
        {
            ListResponse = Result.Ok(new PaginationListResponse<QueryItem>(7,
            [
                new QueryItem
                {
                    Id = first, ClientId = "a", DisplayName = "A", ClientType = nameof(ClientType.Confidential),
                    Enabled = true, IsFirstParty = true, CreatedAt = CreatedAt.UtcDateTime
                },
                new QueryItem
                {
                    Id = second, ClientId = "b", DisplayName = "B", ClientType = nameof(ClientType.Public),
                    Enabled = false, IsFirstParty = false, CreatedAt = CreatedAt.AddDays(-1).UtcDateTime
                }
            ]))
        };

        var result = await new Handler(clientsQuery).Handle(new ListQuery(TenantId, 1, 10, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.RecordsTotal);
        Assert.Equal(
            new[]
            {
                new ClientListItem(first, "a", "A", ClientType.Confidential, true, true, CreatedAt),
                new ClientListItem(second, "b", "B", ClientType.Public, false, false, CreatedAt.AddDays(-1))
            },
            result.Value.Data);
    }
}
