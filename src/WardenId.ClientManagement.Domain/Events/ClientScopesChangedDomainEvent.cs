using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientScopesChangedDomainEvent(Guid ScopeId, bool Assigned) : DomainEvent;
