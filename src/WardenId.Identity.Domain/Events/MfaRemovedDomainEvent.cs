using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>The user removed their own MFA (every factor and recovery code) after a step-up code. No new enrollment is asked for.</summary>
public sealed record MfaRemovedDomainEvent(int RemovedFactorCount, int RemovedRecoveryCodeCount) : DomainEvent;
