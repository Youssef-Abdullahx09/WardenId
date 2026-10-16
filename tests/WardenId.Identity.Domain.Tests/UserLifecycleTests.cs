using WardenId.Identity.Domain.Events;

namespace WardenId.Identity.Domain.Tests;

public class UserLifecycleTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void Disable_FailsWithoutReason()
    {
        var user = CreateUser();

        var result = user.Disable("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Disable_SetsStatusAndRaisesEvent()
    {
        var user = CreateUser();

        var result = user.Disable("policy violation");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserDisabledDomainEvent d && d.Reason == "policy violation");
    }

    [Fact]
    public void Disable_FailsWhenAlreadyDisabled()
    {
        var user = CreateUser();
        user.Disable("first reason");

        var result = user.Disable("second reason");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void SoftDelete_SetsStatusAndRaisesEvent()
    {
        var user = CreateUser();

        var result = user.SoftDelete();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserSoftDeletedDomainEvent);
    }

    [Fact]
    public void SoftDelete_FailsWhenAlreadySoftDeleted()
    {
        var user = CreateUser();
        user.SoftDelete();

        var result = user.SoftDelete();

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Disable_FailsWhenAlreadySoftDeleted()
    {
        var user = CreateUser();
        user.SoftDelete();

        var result = user.Disable("policy violation");

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
    }

    private static User CreateVerifiedUser()
    {
        var user = CreateUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("verify-hash", DateTimeOffset.UtcNow.AddHours(1)));
        user.VerifyEmail("verify-hash");
        return user;
    }

    [Fact]
    public void Enable_ReturnsVerifiedUserToActiveAndRaisesEvent()
    {
        var user = CreateVerifiedUser();
        user.Disable("policy violation");

        var result = user.Enable();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserEnabledDomainEvent);
    }

    [Fact]
    public void Enable_ReturnsUnverifiedUserToPendingVerification()
    {
        var user = CreateUser();
        user.Disable("policy violation");

        var result = user.Enable();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
    }

    [Fact]
    public void Enable_KeepsPendingActions()
    {
        var user = CreateVerifiedUser();
        user.AddRequiredAction(RequiredAction.ResetPassword);
        user.Disable("policy violation");

        user.Enable();

        Assert.Contains(RequiredAction.ResetPassword, user.PendingActions);
    }

    [Fact]
    public void Enable_ClearsLockoutOfAUserDisabledWhileLocked()
    {
        var user = CreateVerifiedUser();
        user.RecordFailedLogin(maxFailedAttempts: 1, lockoutDuration: TimeSpan.FromMinutes(15));
        user.Disable("policy violation");

        user.Enable();

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
        Assert.Null(user.Lockout.LockedUntilUtc);
    }

    [Fact]
    public void Enable_FailsWhenNotDisabled()
    {
        var user = CreateVerifiedUser();

        var result = user.Enable();

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.DoesNotContain(user.DomainEvents, e => e is UserEnabledDomainEvent);
    }

    [Fact]
    public void Enable_FailsWhenSoftDeleted()
    {
        var user = CreateVerifiedUser();
        user.Disable("policy violation");
        user.SoftDelete();

        var result = user.Enable();

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
    }

    [Fact]
    public void Restore_LeavesUserDisabledAndRaisesEvent()
    {
        var user = CreateVerifiedUser();
        user.SoftDelete();

        var result = user.Restore();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserRestoredDomainEvent);
    }

    [Fact]
    public void Restore_ThenEnable_ReturnsUserToActive()
    {
        var user = CreateVerifiedUser();
        user.SoftDelete();
        user.Restore();

        var result = user.Enable();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Restore_FailsWhenNotSoftDeleted()
    {
        var user = CreateVerifiedUser();
        user.Disable("policy violation");

        var result = user.Restore();

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.DoesNotContain(user.DomainEvents, e => e is UserRestoredDomainEvent);
    }
}
