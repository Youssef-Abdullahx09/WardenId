using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Get;

public sealed record UserAttributeSummary(string Key, string Value, UserAttributeValueType ValueType);

public sealed record MfaFactorSummary(Guid Id, MfaFactorType FactorType, bool Enabled);

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
    DateTimeOffset CreatedAt);
