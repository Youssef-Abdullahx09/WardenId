using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain.Events;

public sealed record TenantRenamedDomainEvent(string DisplayName) : DomainEvent;
