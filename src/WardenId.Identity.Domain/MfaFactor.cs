using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

/// <summary>
/// One second factor of a user. A TOTP factor is created unverified and disabled when its enrollment starts, and
/// is enabled by <see cref="Verify"/> once the user proves they hold the secret with a first code.
/// </summary>
public sealed class MfaFactor : Entity
{
    // EF Core materialization only
    private MfaFactor() { }

    private MfaFactor(MfaFactorType factorType, string credentialData, DateTimeOffset enrolledAtUtc)
    {
        FactorType = factorType;
        CredentialData = credentialData;
        Enabled = false;
        EnrolledAtUtc = enrolledAtUtc;
    }

    public MfaFactorType FactorType { get; private set; }

    /// <summary>The factor's secret, protected at rest (for TOTP: the Data Protection-protected shared secret).</summary>
    public string CredentialData { get; private set; }

    public bool Enabled { get; private set; }
    public DateTimeOffset EnrolledAtUtc { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    /// <summary>When a code of this factor last completed a sign-in or the enrollment.</summary>
    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    /// <summary>
    /// The TOTP time step of the last accepted code. A code for this step or an earlier one is refused, so a code
    /// can't be replayed and a code older than the one last used can't be used either.
    /// </summary>
    public long? LastAcceptedTimeStep { get; private set; }

    public bool IsPendingVerification => VerifiedAtUtc is null;

    public static MfaFactor Create(MfaFactorType factorType, string credentialData, DateTimeOffset enrolledAtUtc) =>
        new(factorType, credentialData, enrolledAtUtc);

    /// <summary>Whether a code for <paramref name="timeStep"/> would still be accepted (it's newer than the last one).</summary>
    public bool CanAcceptTimeStep(long timeStep) => LastAcceptedTimeStep is not { } last || timeStep > last;

    /// <summary>Enables the factor with its first code, for <paramref name="timeStep"/>.</summary>
    public Result Verify(long timeStep, DateTimeOffset nowUtc)
    {
        if (VerifiedAtUtc is not null) return Result.Fail(UserErrors.MfaFactorAlreadyVerified());

        VerifiedAtUtc = nowUtc;
        Enabled = true;
        LastAcceptedTimeStep = timeStep;
        LastUsedAtUtc = nowUtc;
        return Result.Ok();
    }

    /// <summary>Records a code accepted at sign-in. Refuses a code for a time step that isn't newer than the last one.</summary>
    public Result AcceptCode(long timeStep, DateTimeOffset nowUtc)
    {
        if (!Enabled) return Result.Fail(UserErrors.MfaFactorNotFound());
        if (!CanAcceptTimeStep(timeStep)) return Result.Fail(UserErrors.MfaCodeInvalid());

        LastAcceptedTimeStep = timeStep;
        LastUsedAtUtc = nowUtc;
        return Result.Ok();
    }
}
