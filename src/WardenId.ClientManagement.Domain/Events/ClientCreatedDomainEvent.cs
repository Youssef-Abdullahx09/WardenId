using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientCreatedDomainEvent(ClientId ClientId) : DomainEvent;
