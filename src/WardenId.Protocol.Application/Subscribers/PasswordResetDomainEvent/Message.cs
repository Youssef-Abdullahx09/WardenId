namespace WardenId.Protocol.Application.Subscribers.PasswordResetDomainEvent;

/// <summary>
/// Identity's PasswordResetDomainEvent as Protocol reads it: only the user id (DomainEvent.AggregateId, a globally unique v7 GUID);
/// the rest of the JSON body is ignored. Protocol never references Identity.Domain.
/// </summary>
public sealed record Message(Guid AggregateId);
