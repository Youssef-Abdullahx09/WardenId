using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>A recovery code stood in for the user's second factor at sign-in. The code itself is never part of it.</summary>
public sealed record RecoveryCodeUsedDomainEvent(int RemainingRecoveryCodes) : DomainEvent;
