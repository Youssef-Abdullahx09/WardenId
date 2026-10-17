using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Get;

public sealed record UserAttributeSummary(string Key, string Value, UserAttributeValueType ValueType);

/// <param name="VerifiedAtUtc">Null while the enrollment hasn't been completed (the factor isn't enabled yet).</param>
public sealed record MfaFactorSummary(
    Guid Id,
    MfaFactorType FactorType,
    bool Enabled,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? VerifiedAtUtc,
    DateTimeOffset? LastUsedAtUtc);

public sealed record UserDetail(
    Guid Id,
    string Email,
    string Username,
    UserStatus Status,
    int FailedLoginAttemptCount,
    DateTimeOffset? LockedUntilUtc,
    IReadOnlyCollection<RequiredAction> PendingActions,
    IReadOnlyCollection<UserAttributeSummary> Attributes,
    IReadOnlyCollection<MfaFactorSummary> MfaFactors,
    int RemainingRecoveryCodes,
    DateTimeOffset CreatedAt);
