using WardenId.Identity.Application.Features.Users.VerifyEmail;
using WardenId.Identity.Application.Utilities;

namespace WardenId.Identity.Application.Tests.Features.Users.VerifyEmail;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ActivatesUser_WhenTokenIsValid()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var rawToken = "raw-token";
        user.AddEmailVerificationToken(EmailVerificationToken.Create(SecureTokenGenerator.Hash(rawToken), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        // Seeded with a different actor so the post-Handle actor assertion proves the handler set it.
        await repository.CreateAsync(user, Guid.NewGuid(), CancellationToken.None);

        var handler = new Handler(repository);
        var result = await handler.Handle(new Command(tenantId, user.Id, rawToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(user.Id, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenTokenDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.AddEmailVerificationToken(EmailVerificationToken.Create(SecureTokenGenerator.Hash("correct-token"), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository);
        var result = await handler.Handle(new Command(tenantId, user.Id, "wrong-token"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
