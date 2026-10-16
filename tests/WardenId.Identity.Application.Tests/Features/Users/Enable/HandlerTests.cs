using WardenId.Identity.Application.Features.Users.Enable;

namespace WardenId.Identity.Application.Tests.Features.Users.Enable;

public class HandlerTests
{
    [Fact]
    public async Task Handle_EnablesDisabledUser()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.Disable("policy violation");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(actorUserId, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_FailsWithNotFound_WhenUserDoesNotExist()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsAndDoesNotPersist_WhenUserIsNotDisabled()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
