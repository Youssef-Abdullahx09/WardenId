using WardenId.Identity.Application.Features.Users.Disable;

namespace WardenId.Identity.Application.Tests.Features.Users.Disable;

public class HandlerTests
{
    [Fact]
    public async Task Handle_DisablesUser_WithReason()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, "policy violation", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenReasonIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, "", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
