using WardenId.Tenancy.Application.Features.Tenants.ResolveForIssuance;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.ResolveForIssuance;

public class HandlerTests
{
    private static async Task<Result<TenantForIssuance>> ResolveAsync(Tenant tenant, string slug)
    {
        await using var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await new Handler(db).Handle(new Query(slug), CancellationToken.None);
    }

    [Theory]
    [InlineData(Status.Active, TenantIssuanceStatus.Active)]
    [InlineData(Status.Suspended, TenantIssuanceStatus.Suspended)]
    public async Task Handle_ReturnsTenant_WithIssuanceStatus(Status status, TenantIssuanceStatus expected)
    {
        var (tenant, _) = TenantBuilder.Create("acme", status);

        var result = await ResolveAsync(tenant, "acme");

        Assert.True(result.IsSuccess);
        Assert.Equal(tenant.Id, result.Value.TenantId);
        Assert.Equal("acme", result.Value.Slug);
        Assert.Equal(expected, result.Value.Status);
    }

    [Theory]
    [InlineData(Status.Provisioning)]
    [InlineData(Status.Archived)]
    public async Task Handle_ReturnsNotFound_ForNonServableStatus(Status status)
    {
        var (tenant, _) = TenantBuilder.Create("acme", status);
        AssertNotFound(await ResolveAsync(tenant, "acme"));
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForSoftDeletedTenant()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);
        tenant.MarkAsDeleted(Guid.Empty);
        AssertNotFound(await ResolveAsync(tenant, "acme"));
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownSlug()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);
        AssertNotFound(await ResolveAsync(tenant, "other"));
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForBlankSlug()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);
        AssertNotFound(await ResolveAsync(tenant, " "));
    }

    private static void AssertNotFound(Result<TenantForIssuance> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
