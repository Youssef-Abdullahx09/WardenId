using WardenId.Identity.Application.Features.Users.RequiredActions.Clear;

namespace WardenId.Identity.Application.Tests.Features.Users.RequiredActions.Clear;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ClearsRequiredAction()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, RequiredAction.VerifyEmail, actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(RequiredAction.VerifyEmail, user.PendingActions);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(actorUserId, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsSoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SoftDelete();
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, RequiredAction.VerifyEmail, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains(RequiredAction.VerifyEmail, user.PendingActions);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
