using WardenId.Identity.Application.Features.Users.Unlock;

namespace WardenId.Identity.Application.Tests.Features.Users.Unlock;

public class HandlerTests
{
    [Fact]
    public async Task Handle_UnlocksUser_WhenCurrentlyLocked()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.RecordFailedLogin(1, TimeSpan.FromMinutes(15));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsNotLocked()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
