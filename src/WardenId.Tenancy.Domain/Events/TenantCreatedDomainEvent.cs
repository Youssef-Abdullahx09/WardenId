using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain.Events;
 
public sealed record TenantCreatedDomainEvent(Slug Slug) : DomainEvent;