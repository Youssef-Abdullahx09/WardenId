using WardenId.Identity.Application.Features.Users.List;

namespace WardenId.Identity.Application.Tests.Features.Users.List;

public class HandlerTests
{
    private static async Task<(string DatabaseName, Guid TenantId)> SeedThreeUsersAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        using var seedContext = TestIdentityDbContext.CreateNew(databaseName);
        for (var i = 0; i < 3; i++)
        {
            var user = TestUsers.CreatePendingVerification(tenantId, $"someone{i}@example.com", $"someone{i}");
            user.MarkAsCreated(Guid.NewGuid());
            seedContext.Users.Add(user);
        }
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return (databaseName, tenantId);
    }

    [Fact]
    public async Task Handle_ReturnsTotalCountAndRequestedPage()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 2, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecordsTotal);
        Assert.Equal(2, result.Value.Data.Count());
    }

    [Fact]
    public async Task Handle_ReturnsSecondPage()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 2, PageSize: 2, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Data);
    }

    [Fact]
    public async Task Handle_FiltersByStatus()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 10, Status: UserStatus.Active), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.RecordsTotal);
    }

    [Fact]
    public async Task Handle_ExcludesOtherTenants()
    {
        var (databaseName, _) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), PageNumber: 1, PageSize: 10, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.RecordsTotal);
    }
}
