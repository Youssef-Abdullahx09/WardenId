using WardenId.Identity.Application.Mfa;

namespace WardenId.Identity.Application.Tests;

public static class TestMfa
{
    // The real clock, not a fixed date: User.RecordFailedLogin sets a lockout from the system clock, which the
    // handlers compare with their TimeProvider's time.
    public static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    /// <summary>The RFC 6238 test secret ("12345678901234567890").</summary>
    public static byte[] Secret => "12345678901234567890"u8.ToArray();

    /// <summary>Enrolls an enabled TOTP factor over <paramref name="secret"/> whose first code was a minute before <see cref="Now"/>.</summary>
    public static MfaFactor AddEnabledTotp(this User user, byte[]? secret = null, IEnumerable<RecoveryCode>? recoveryCodes = null)
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, new FakeTotpSecretProtector().Protect(secret ?? Secret), Now.AddMinutes(-2));
        Assert.True(user.BeginMfaEnrollment(factor).IsSuccess);
        Assert.True(user.CompleteMfaEnrollment(factor.Id, Totp.TimeStepAt(Now.AddMinutes(-1)), recoveryCodes ?? [], Now.AddMinutes(-1)).IsSuccess);
        return factor;
    }

    public static string CodeAt(DateTimeOffset time, byte[]? secret = null) =>
        Totp.ComputeCode(secret ?? Secret, Totp.TimeStepAt(time));
}
