using WardenId.Tenancy.Application.Features.Tenants.List;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.List;

public class HandlerTests
{
    private static async Task<TestTenancyDbContext> SeedAsync(params (string Slug, Status Status)[] tenants)
    {
        var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        foreach (var (slug, status) in tenants)
            db.Tenants.Add(TenantBuilder.Create(slug, status).Tenant);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Handle_PagesAllTenants()
    {
        await using var db = await SeedAsync(("acme", Status.Active), ("globex", Status.Active), ("initech", Status.Suspended));

        var result = await new Handler(db).Handle(new Query(1, 2, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecordsTotal);
        Assert.Equal(2, result.Value.Data.Count());
    }

    [Fact]
    public async Task Handle_FiltersByStatus()
    {
        await using var db = await SeedAsync(("acme", Status.Active), ("initech", Status.Suspended));

        var result = await new Handler(db).Handle(new Query(1, 25, Status.Suspended), CancellationToken.None);

        var item = Assert.Single(result.Value.Data);
        Assert.Equal("initech", item.Slug);
        Assert.Equal(Status.Suspended, item.Status);
        Assert.Equal("Display initech", item.DisplayName);
    }
}
