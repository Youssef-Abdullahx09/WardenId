using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>An admin removed every MFA factor and recovery code of the user, who has to enroll again.</summary>
public sealed record MfaResetDomainEvent(int RemovedFactorCount, int RemovedRecoveryCodeCount) : DomainEvent;
