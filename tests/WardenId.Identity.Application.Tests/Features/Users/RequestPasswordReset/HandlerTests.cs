using WardenId.Identity.Application.Features.Users.RequestPasswordReset;

namespace WardenId.Identity.Application.Tests.Features.Users.RequestPasswordReset;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRawToken_WhenEmailMatchesAUser()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        var repository = new FakeUserRepository();
        // Seeded with a different actor so the post-Handle actor assertion proves the handler set it.
        await repository.CreateAsync(user, Guid.NewGuid(), CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, "someone@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.RawResetToken));
        Assert.Single(user.PasswordResetTokens);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(user.Id, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithNullPayload_WhenEmailDoesNotMatch()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), "nobody@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    // A disabled/soft-deleted target must be indistinguishable from "no such user" — same success-with-null
    // shape, and no live reset token minted for an account that cannot authenticate.
    [Fact]
    public async Task Handle_ReturnsSuccessWithNullPayload_WhenUserIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        user.Disable("policy violation");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, "someone@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Empty(user.PasswordResetTokens);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithNullPayload_WhenUserIsSoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        user.SoftDelete();
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, "someone@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Empty(user.PasswordResetTokens);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
