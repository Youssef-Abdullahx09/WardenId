using WardenId.Identity.Domain.Events;

namespace WardenId.Identity.Domain.Tests;

public class UserPasswordResetTests
{
    private static User CreateActiveUserWithCredential()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        user.AddEmailVerificationToken(EmailVerificationToken.Create("verify-hash", DateTimeOffset.UtcNow.AddHours(1)));
        user.VerifyEmail("verify-hash");
        user.SetPassword(PasswordCredential.Create("hash-old", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);
        return user;
    }

    [Fact]
    public void RequestPasswordReset_AddsTokenAndRaisesEvent()
    {
        var user = CreateActiveUserWithCredential();

        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        Assert.Single(user.PasswordResetTokens);
        Assert.Contains(user.DomainEvents, e => e is PasswordResetRequestedDomainEvent);
    }

    [Fact]
    public void ResetPassword_FailsWhenTokenDoesNotMatch()
    {
        var user = CreateActiveUserWithCredential();
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("wrong-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void ResetPassword_SucceedsAndSetsNewCredential()
    {
        var user = CreateActiveUserWithCredential();
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("reset-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-new", user.Credential!.Hash);
        Assert.Contains(user.DomainEvents, e => e is PasswordResetDomainEvent);
    }

    [Fact]
    public void ResetPassword_ClearsLockoutAndReactivatesLockedUser()
    {
        var user = CreateActiveUserWithCredential();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("reset-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public void ResetPassword_FailsWhenUserIsDisabled()
    {
        var user = CreateActiveUserWithCredential();
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));
        user.Disable("policy violation");

        var result = user.ResetPassword("reset-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.Disabled, user.Status);
    }

    [Fact]
    public void ResetPassword_FailsWhenUserIsSoftDeleted()
    {
        var user = CreateActiveUserWithCredential();
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));
        user.SoftDelete();

        var result = user.ResetPassword("reset-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
    }

    [Fact]
    public void ResetPassword_ClearsPendingResetPasswordAction()
    {
        var user = CreateActiveUserWithCredential();
        user.AddRequiredAction(RequiredAction.ResetPassword);
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("reset-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(RequiredAction.ResetPassword, user.PendingActions);
    }

    [Fact]
    public void ResetPassword_KeepsResetPasswordAction_WhenTokenIsInvalid()
    {
        var user = CreateActiveUserWithCredential();
        user.AddRequiredAction(RequiredAction.ResetPassword);
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("wrong-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsFailed);
        Assert.Contains(RequiredAction.ResetPassword, user.PendingActions);
    }
}
