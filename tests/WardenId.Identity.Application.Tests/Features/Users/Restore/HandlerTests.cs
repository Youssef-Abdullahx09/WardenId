using WardenId.Identity.Application.Features.Users.Restore;

namespace WardenId.Identity.Application.Tests.Features.Users.Restore;

public class HandlerTests
{
    private static async Task<(FakeUserRepository Repository, User User)> SeedSoftDeletedUserAsync(Guid tenantId)
    {
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SoftDelete();
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        return (repository, user);
    }

    [Fact]
    public async Task Handle_RestoresSoftDeletedUserAsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var (repository, user) = await SeedSoftDeletedUserAsync(tenantId);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.False(user.IsDeleted);
        Assert.Equal(1, repository.RestoreCallCount);
        Assert.Equal(actorUserId, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_RestoredUserIsFoundAgain()
    {
        var tenantId = Guid.NewGuid();
        var (repository, user) = await SeedSoftDeletedUserAsync(tenantId);
        var handler = new Handler(repository);

        await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        var lookup = await repository.GetByIdAsync(tenantId, user.Id, CancellationToken.None);
        Assert.True(lookup.IsSuccess);
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
    public async Task Handle_FailsWithNotFound_WhenUserBelongsToAnotherTenant()
    {
        var (repository, user) = await SeedSoftDeletedUserAsync(Guid.NewGuid());
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(Guid.NewGuid(), user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(0, repository.RestoreCallCount);
    }

    [Fact]
    public async Task Handle_FailsAndDoesNotPersist_WhenUserIsNotSoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.Disable("policy violation");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(0, repository.RestoreCallCount);
    }
}
