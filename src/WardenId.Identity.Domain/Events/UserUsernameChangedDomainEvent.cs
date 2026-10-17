using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserUsernameChangedDomainEvent(Username PreviousUsername, Username Username) : DomainEvent;
