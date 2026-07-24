using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain.Events;

public sealed record TenantSuspendedDomainEvent(string Reason) : DomainEvent;