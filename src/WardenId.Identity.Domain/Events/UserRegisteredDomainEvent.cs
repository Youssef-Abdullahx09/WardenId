using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserRegisteredDomainEvent(Email Email, Username Username) : DomainEvent;
