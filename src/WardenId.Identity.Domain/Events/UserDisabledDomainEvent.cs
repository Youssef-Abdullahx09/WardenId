using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserDisabledDomainEvent(string Reason) : DomainEvent;
