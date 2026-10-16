using WardenId.Identity.Application.Features.Users.RemoveAttribute;

namespace WardenId.Identity.Application.Tests.Features.Users.RemoveAttribute;

public class HandlerTests
{
    private static async Task<(User User, FakeUserRepository Repository)> SeedUserWithAttributeAsync(Guid tenantId)
    {
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SetAttribute("department", "engineering", UserAttributeValueType.String);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        return (user, repository);
    }

    [Fact]
    public async Task Handle_RemovesAttribute_AndSavesAsTheActor()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var (user, repository) = await SeedUserWithAttributeAsync(tenantId);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, "department", actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.Attributes);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(actorUserId, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_FailsWithNotFound_WhenKeyDoesNotExist()
    {
        var tenantId = Guid.NewGuid();
        var (user, repository) = await SeedUserWithAttributeAsync(tenantId);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, "costCenter", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Single(user.Attributes);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_FailsWithNotFound_WhenUserIsInAnotherTenant()
    {
        var (user, repository) = await SeedUserWithAttributeAsync(Guid.NewGuid());
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(Guid.NewGuid(), user.Id, "department", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Single(user.Attributes);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var (user, repository) = await SeedUserWithAttributeAsync(tenantId);
        user.Disable("policy violation");
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, "department", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.Attributes);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
