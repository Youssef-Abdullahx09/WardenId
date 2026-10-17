using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>A tenant's identity policy overrides were set or changed. Carries the tenant id explicitly (AggregateId isn't deserialized).</summary>
public sealed record IdentityTenantPolicyChangedDomainEvent(Guid TenantId) : DomainEvent;
