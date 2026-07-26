using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record AccountLockedOutDomainEvent(DateTimeOffset LockedUntilUtc) : DomainEvent;
