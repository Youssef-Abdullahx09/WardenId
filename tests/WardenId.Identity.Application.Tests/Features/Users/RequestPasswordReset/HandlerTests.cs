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
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, "someone@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.RawResetToken));
        Assert.Single(user.PasswordResetTokens);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithNullPayload_WhenEmailDoesNotMatch()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), "nobody@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
