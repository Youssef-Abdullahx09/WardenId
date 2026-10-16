using WardenId.Identity.Application.Features.Users.SendPasswordReset;

namespace WardenId.Identity.Application.Tests.Features.Users.SendPasswordReset;

public class HandlerTests
{
    [Fact]
    public async Task Handle_MintsTokenAndEmailsIt_WithAdminAsActor()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com", "someone");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var emailSender = new FakeIdentityEmailSender();
        var handler = new Handler(repository, emailSender);

        var result = await handler.Handle(new Command(tenantId, user.Id, actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var token = Assert.Single(user.PasswordResetTokens);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(actorUserId, repository.LastActorId!.Value);
        var email = Assert.Single(emailSender.SentPasswordResetEmails);
        Assert.Equal("someone@example.com", email.ToEmail);
        Assert.Equal("someone", email.Username);
        Assert.Equal(SecureTokenGenerator.Hash(email.RawToken), token.TokenHash);
        Assert.Equal(token.ExpiresAtUtc, email.ExpiresAtUtc);
        Assert.InRange(email.ExpiresAtUtc - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task Handle_FailsWithNotFound_WhenUserDoesNotExist()
    {
        var emailSender = new FakeIdentityEmailSender();
        var handler = new Handler(new FakeUserRepository(), emailSender);

        var result = await handler.Handle(new Command(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Empty(emailSender.SentPasswordResetEmails);
    }

    [Fact]
    public async Task Handle_FailsWithoutSending_WhenUserIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.Disable("policy violation");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var emailSender = new FakeIdentityEmailSender();
        var handler = new Handler(repository, emailSender);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Empty(user.PasswordResetTokens);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Empty(emailSender.SentPasswordResetEmails);
    }

    [Fact]
    public async Task Handle_FailsWithoutSending_WhenUserIsSoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SoftDelete();
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var emailSender = new FakeIdentityEmailSender();
        var handler = new Handler(repository, emailSender);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(user.PasswordResetTokens);
        Assert.Empty(emailSender.SentPasswordResetEmails);
    }
}
