using FluentResults;
using WardenId.Identity.Domain.Events;

namespace WardenId.Identity.Domain.Tests;

public class UserLoginLockoutTests
{
    private static User CreateActiveUser()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));
        user.VerifyEmail("hash-1");
        return user;
    }

    [Fact]
    public void RecordFailedLogin_IncrementsFailedAttemptCount()
    {
        var user = CreateActiveUser();

        user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void RecordFailedLogin_LocksAccountAtThreshold()
    {
        var user = CreateActiveUser();

        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }

        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.Lockout.LockedUntilUtc);
    }

    [Fact]
    public void RecordFailedLogin_RaisesAccountLockedOutDomainEventOnlyAtThreshold()
    {
        var user = CreateActiveUser();

        for (var i = 0; i < 4; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }
        Assert.DoesNotContain(user.DomainEvents, e => e is AccountLockedOutDomainEvent);

        user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        Assert.Contains(user.DomainEvents, e => e is AccountLockedOutDomainEvent);
    }

    [Fact]
    public void RecordSuccessfulLogin_ResetsLockoutState()
    {
        var user = CreateActiveUser();
        user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        user.RecordSuccessfulLogin();

        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public void Unlock_FailsWhenUserIsNotLocked()
    {
        var user = CreateActiveUser();

        var result = user.Unlock();

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Unlock_ReactivatesLockedUser()
    {
        var user = CreateActiveUser();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }

        var result = user.Unlock();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public void RecordFailedLogin_FailsWhenUserIsSoftDeleted()
    {
        var user = CreateActiveUser();
        user.SoftDelete();

        var result = user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RecordFailedLogin_FailsWhenUserIsDisabled()
    {
        var user = CreateActiveUser();
        user.Disable("policy violation");

        var result = user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void SoftDelete_ThenRecordFailedLoginThenUnlock_DoesNotReactivateUser()
    {
        var user = CreateActiveUser();
        user.SoftDelete();

        Result? lastResult = null;
        for (var i = 0; i < 5; i++)
        {
            lastResult = user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }

        Assert.True(lastResult!.IsFailed);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);

        var unlockResult = user.Unlock();
        Assert.True(unlockResult.IsFailed);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
    }
}
