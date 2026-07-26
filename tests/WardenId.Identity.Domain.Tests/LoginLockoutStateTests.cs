namespace WardenId.Identity.Domain.Tests;

public class LoginLockoutStateTests
{
    [Fact]
    public void None_HasZeroFailedAttemptsAndNoLock()
    {
        Assert.Equal(0, LoginLockoutState.None.FailedAttemptCount);
        Assert.Null(LoginLockoutState.None.LockedUntilUtc);
    }

    [Fact]
    public void FromTrustedSource_SetsBothFields()
    {
        var lockedUntil = DateTimeOffset.UtcNow.AddMinutes(15);

        var state = LoginLockoutState.FromTrustedSource(3, lockedUntil);

        Assert.Equal(3, state.FailedAttemptCount);
        Assert.Equal(lockedUntil, state.LockedUntilUtc);
    }

    [Fact]
    public void TwoStatesWithSameValues_AreEqual()
    {
        var a = LoginLockoutState.FromTrustedSource(2, null);
        var b = LoginLockoutState.FromTrustedSource(2, null);

        Assert.Equal(a, b);
    }
}
