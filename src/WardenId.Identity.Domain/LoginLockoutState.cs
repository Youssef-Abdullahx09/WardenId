using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class LoginLockoutState : ValueObject
{
    public static readonly LoginLockoutState None = new(0, null);

    public int FailedAttemptCount { get; }
    public DateTimeOffset? LockedUntilUtc { get; }

    private LoginLockoutState(int failedAttemptCount, DateTimeOffset? lockedUntilUtc)
    {
        FailedAttemptCount = failedAttemptCount;
        LockedUntilUtc = lockedUntilUtc;
    }

    public static LoginLockoutState FromTrustedSource(int failedAttemptCount, DateTimeOffset? lockedUntilUtc)
        => new(failedAttemptCount, lockedUntilUtc);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FailedAttemptCount;
        yield return LockedUntilUtc;
    }
}
