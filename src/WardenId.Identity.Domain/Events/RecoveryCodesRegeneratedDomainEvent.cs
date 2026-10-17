using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>The user's recovery codes were replaced with a new set of <paramref name="Count"/>. The codes are never part of it.</summary>
public sealed record RecoveryCodesRegeneratedDomainEvent(int Count) : DomainEvent;
