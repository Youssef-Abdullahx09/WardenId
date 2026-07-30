using WardenId.Identity.Application.Features.Users.RequestEmailVerification;

namespace WardenId.Identity.Application.Tests.Features.Users.RequestEmailVerification;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRawToken_AndAddsTokenToUser()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository);
        var result = await handler.Handle(new Command(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawVerificationToken));
        Assert.Single(user.EmailVerificationTokens);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserNotFound()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_AndMintsNoToken_WhenUserIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.Disable("policy violation");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository);
        var result = await handler.Handle(new Command(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(user.EmailVerificationTokens);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
