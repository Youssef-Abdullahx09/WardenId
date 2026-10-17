using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>The user accepted the tenant's terms of service.</summary>
public sealed record UserTermsAcceptedDomainEvent(DateTimeOffset AcceptedAtUtc) : DomainEvent;
