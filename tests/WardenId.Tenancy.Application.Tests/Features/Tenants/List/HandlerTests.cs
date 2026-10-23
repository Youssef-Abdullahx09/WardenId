using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Application.Features.Tenants.List;
using QueryItem = WardenId.Tenancy.Query.Contracts.Features.Tenants.List.Item;
using TenantsListQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.List.Query;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.List;

// A bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Application.Features.Tenants.List.Query;

public class HandlerTests
{
    [Fact]
    public async Task Handle_PassesPageAndNoStatus_ToTheQueryLayer()
    {
        var tenantsQuery = new FakeTenantsQuery();

        await new Handler(tenantsQuery).Handle(new Query(2, 25, null), CancellationToken.None);

        Assert.Equal(new TenantsListQuery(2, 25, null), Assert.Single(tenantsQuery.ListCalls));
    }

    [Fact]
    public async Task Handle_PassesTheStatusFilter_ByMemberName()
    {
        var tenantsQuery = new FakeTenantsQuery();

        await new Handler(tenantsQuery).Handle(new Query(1, 25, Status.Suspended), CancellationToken.None);

        Assert.Equal(nameof(Status.Suspended), Assert.Single(tenantsQuery.ListCalls).Status);
    }

    [Fact]
    public async Task Handle_MapsRows_InQueryOrder_WithTotal()
    {
        var createdAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var initech = Guid.NewGuid();
        var tenantsQuery = new FakeTenantsQuery
        {
            ListResponse = Result.Ok(new PaginationListResponse<QueryItem>(3,
            [
                new QueryItem
                {
                    Id = initech, Slug = "initech", DisplayName = "Display initech",
                    Status = nameof(Status.Suspended), CreatedAt = createdAt
                },
                new QueryItem
                {
                    Id = Guid.NewGuid(), Slug = "acme", DisplayName = "Display acme",
                    Status = nameof(Status.Active), CreatedAt = createdAt.AddDays(-1)
                }
            ]))
        };

        var result = await new Handler(tenantsQuery).Handle(new Query(1, 2, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecordsTotal);
        Assert.Equal(
            new TenantListItem(initech, "initech", "Display initech", Status.Suspended, new DateTimeOffset(createdAt)),
            result.Value.Data.First());
        Assert.Equal(["initech", "acme"], result.Value.Data.Select(i => i.Slug));
    }
}
