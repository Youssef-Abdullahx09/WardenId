using WardenId.Tenancy.Application.Features.Tenants.GetBySlug;
using TenantRow = WardenId.Tenancy.Query.Contracts.Features.Tenants.Get.Item;
using TenantsGetBySlugQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.GetBySlug.Query;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.GetBySlug;

// A bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Application.Features.Tenants.GetBySlug.Query;

public class HandlerTests
{
    [Fact]
    public async Task Handle_FindsTheTenant()
    {
        var tenantsQuery = new FakeTenantsQuery
        {
            GetBySlugResponse = Result.Ok<TenantRow?>(new TenantRow
            {
                Id = Guid.NewGuid(), Slug = "acme", DisplayName = "Acme", Status = nameof(Status.Active),
                CreatedAt = DateTime.UtcNow
            })
        };

        var result = await new Handler(tenantsQuery).Handle(new Query("acme"), CancellationToken.None);

        Assert.Equal("acme", result.Value.Slug);
        Assert.Equal(Status.Active, result.Value.Status);
        Assert.Equal(new TenantsGetBySlugQuery("acme"), Assert.Single(tenantsQuery.GetBySlugCalls));
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownSlug()
    {
        var result = await new Handler(new FakeTenantsQuery()).Handle(new Query("globex"), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForMalformedSlug_WithoutQuerying()
    {
        var tenantsQuery = new FakeTenantsQuery();

        var result = await new Handler(tenantsQuery).Handle(new Query("Not A Slug!"), CancellationToken.None);

        AssertNotFound(result);
        Assert.Empty(tenantsQuery.GetBySlugCalls);
    }

    private static void AssertNotFound(Result<WardenId.Tenancy.Application.Features.Tenants.Get.TenantDetail> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
