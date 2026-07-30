using WardenId.Identity.Application.Features.Users.SoftDelete;

namespace WardenId.Identity.Application.Tests.Features.Users.SoftDelete;

public class HandlerTests
{
    [Fact]
    public async Task Handle_SoftDeletesUser()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
        Assert.Equal(1, repository.SoftDeleteCallCount);
        Assert.Equal(actorUserId, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenAlreadySoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SoftDelete();
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, repository.SoftDeleteCallCount);
    }
}
