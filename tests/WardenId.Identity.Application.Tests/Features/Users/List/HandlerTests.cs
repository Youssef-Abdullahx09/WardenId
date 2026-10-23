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

    [Fact]
    public async Task Handle_ExcludesSoftDeletedUsers_WhenNoStatusFilter()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAndOneSoftDeletedAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 10, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecordsTotal);
    }

    [Fact]
    public async Task Handle_ReturnsSoftDeletedUsers_WhenFilteredBySoftDeleted()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAndOneSoftDeletedAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 10, Status: UserStatus.SoftDeleted), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RecordsTotal);
        Assert.Equal("deleted@example.com", Assert.Single(result.Value.Data).Email);
    }

    private static async Task<(string DatabaseName, Guid TenantId)> SeedThreeUsersAndOneSoftDeletedAsync()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAsync();

        using var seedContext = TestIdentityDbContext.CreateNew(databaseName);
        var deleted = TestUsers.CreatePendingVerification(tenantId, "deleted@example.com", "deleted");
        deleted.SoftDelete();
        seedContext.Users.Add(deleted);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return (databaseName, tenantId);
    }

    // Search, and sorting by email or username, compare the stored Email/Username columns, which
    // EF InMemory can't evaluate (it sees the value objects); WardenId.Identity.Infrastructure.Tests
    // covers them on Postgres. These tests cover what doesn't depend on the provider.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ReturnsEveryone_WhenSearchIsBlank(string? search)
    {
        var (databaseName, tenantId) = await SeedNamedUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 10, Status: null, Search: search), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecordsTotal);
    }

    [Fact]
    public async Task Handle_FailsValidation_WhenSearchIsLongerThanAnEmail()
    {
        var (databaseName, tenantId) = await SeedNamedUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(
            new Query(tenantId, PageNumber: 1, PageSize: 10, Status: null, Search: new string('a', Email.MaxLength + 1)),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData(null, new[] { "carol@example.com", "bob@corp.test", "alice@example.com" })] // newest first
    [InlineData("-createdAt", new[] { "carol@example.com", "bob@corp.test", "alice@example.com" })]
    [InlineData("createdAt", new[] { "alice@example.com", "bob@corp.test", "carol@example.com" })]
    [InlineData("CREATEDAT", new[] { "alice@example.com", "bob@corp.test", "carol@example.com" })]
    public async Task Handle_SortsByCreatedAt(string? sort, string[] expectedEmails)
    {
        var (databaseName, tenantId) = await SeedNamedUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 10, Status: null, Sort: sort), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedEmails, result.Value.Data.Select(u => u.Email));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("-")]
    [InlineData("--email")]
    [InlineData("email desc")]
    [InlineData("2")]
    public async Task Handle_FailsValidation_WhenSortIsUnknown(string sort)
    {
        var (databaseName, tenantId) = await SeedNamedUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 10, Status: null, Sort: sort), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    /// <summary>Alice, then Bob, then Carol, each created a minute after the previous one.</summary>
    private static async Task<(string DatabaseName, Guid TenantId)> SeedNamedUsersAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);

        using var seedContext = TestIdentityDbContext.CreateNew(databaseName);
        foreach (var (email, username) in new[] { ("alice@example.com", "Alice.Smith"), ("bob@corp.test", "bobby"), ("carol@example.com", "c_ross") })
        {
            var user = TestUsers.CreatePendingVerification(tenantId, email, username);
            seedContext.Users.Add(user);
            seedContext.Entry(user).Property(u => u.CreatedAt).CurrentValue = createdAt;
            createdAt = createdAt.AddMinutes(1);
        }
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return (databaseName, tenantId);
    }
}
