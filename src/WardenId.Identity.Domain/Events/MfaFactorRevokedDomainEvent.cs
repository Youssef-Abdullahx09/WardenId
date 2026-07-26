using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record MfaFactorRevokedDomainEvent(Guid MfaFactorId) : DomainEvent;
