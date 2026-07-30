using WardenId.Identity.Application.Features.Users.Get;

namespace WardenId.Identity.Application.Tests.Features.Users.Get;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsUserDetail_WhenUserExists()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com", "someone");
        user.MarkAsCreated(Guid.NewGuid());

        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("someone@example.com", result.Value.Email);
        Assert.Equal("someone", result.Value.Username);
        Assert.Equal(UserStatus.PendingVerification, result.Value.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserDoesNotExist()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserBelongsToADifferentTenant()
    {
        var databaseName = Guid.NewGuid().ToString();
        var user = TestUsers.CreatePendingVerification(Guid.NewGuid());
        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
