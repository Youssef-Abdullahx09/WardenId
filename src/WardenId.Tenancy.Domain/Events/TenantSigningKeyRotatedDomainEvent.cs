using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain.Events;

/// <summary>SigningKeyId now signs; PreviousSigningKeyId (if any) is Retiring.</summary>
public sealed record TenantSigningKeyRotatedDomainEvent(Guid SigningKeyId, Guid? PreviousSigningKeyId) : DomainEvent;
