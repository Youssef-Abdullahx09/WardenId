using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain.Events;

public sealed record TenantSigningKeyAssignedDomainEvent(Guid SigningKeyId) : DomainEvent;