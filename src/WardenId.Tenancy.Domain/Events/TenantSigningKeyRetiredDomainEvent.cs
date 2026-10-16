using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain.Events;

/// <summary>SigningKeyId is no longer published; tokens it signed stop verifying.</summary>
public sealed record TenantSigningKeyRetiredDomainEvent(Guid SigningKeyId) : DomainEvent;
