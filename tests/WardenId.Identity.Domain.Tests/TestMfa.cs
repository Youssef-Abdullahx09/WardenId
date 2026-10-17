namespace WardenId.Identity.Domain.Tests;

internal static class TestMfa
{
    public static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Starts a TOTP enrollment for the user and returns the unverified factor.</summary>
    public static MfaFactor AddPendingTotp(this User user, string credentialData = "protected-secret")
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, credentialData, Now);
        Assert.True(user.BeginMfaEnrollment(factor).IsSuccess);
        return factor;
    }

    /// <summary>Enrolls an enabled TOTP factor (its first code was for <paramref name="timeStep"/>).</summary>
    public static MfaFactor AddEnabledTotp(
        this User user, IEnumerable<RecoveryCode>? recoveryCodes = null, string credentialData = "protected-secret", long timeStep = 100)
    {
        var factor = user.AddPendingTotp(credentialData);
        Assert.True(user.CompleteMfaEnrollment(factor.Id, timeStep, recoveryCodes ?? [], Now).IsSuccess);
        return factor;
    }
}
