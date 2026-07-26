using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record MfaFactorEnrolledDomainEvent(Guid MfaFactorId, MfaFactorType FactorType) : DomainEvent;
