using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record LoginFailedDomainEvent(int FailedAttemptCount) : DomainEvent;
