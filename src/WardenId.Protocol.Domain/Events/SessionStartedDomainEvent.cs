using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain.Events;

// Ids are explicit record parameters: DomainEvent.UserId/AggregateId have private setters and are not deserialized by CAP subscribers.
public sealed record SessionStartedDomainEvent(Guid Sid, Guid TenantId, Guid SubjectId) : DomainEvent;
