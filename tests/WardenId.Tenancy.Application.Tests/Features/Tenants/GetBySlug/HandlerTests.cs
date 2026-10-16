using WardenId.Tenancy.Application.Features.Tenants.GetBySlug;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.GetBySlug;

public class HandlerTests
{
    private static async Task<Result<WardenId.Tenancy.Application.Features.Tenants.Get.TenantDetail>> GetAsync(string slug)
    {
        await using var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        db.Tenants.Add(TenantBuilder.Create("acme", Status.Active).Tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await new Handler(new FakeTenantRepository(db)).Handle(new Query(slug), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_FindsTheTenant() => Assert.Equal("acme", (await GetAsync("acme")).Value.Slug);

    [Theory]
    [InlineData("globex")]
    [InlineData("Not A Slug!")]
    public async Task Handle_ReturnsNotFound_ForUnknownOrMalformedSlug(string slug)
    {
        var result = await GetAsync(slug);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
