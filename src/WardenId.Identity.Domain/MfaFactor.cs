using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class MfaFactor : Entity
{
    // EF Core materialization only
    private MfaFactor() { }

    private MfaFactor(MfaFactorType factorType, string credentialData)
    {
        FactorType = factorType;
        CredentialData = credentialData;
        Enabled = false;
        EnrolledAtUtc = DateTimeOffset.UtcNow;
    }

    public MfaFactorType FactorType { get; private set; }
    public string CredentialData { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset EnrolledAtUtc { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public static MfaFactor Create(MfaFactorType factorType, string credentialData) =>
        new(factorType, credentialData);

    public Result Verify()
    {
        if (VerifiedAtUtc is not null) return Result.Fail(UserErrors.MfaFactorAlreadyVerified());

        VerifiedAtUtc = DateTimeOffset.UtcNow;
        Enabled = true;
        return Result.Ok();
    }
}
