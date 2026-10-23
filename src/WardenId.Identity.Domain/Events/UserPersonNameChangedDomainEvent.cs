using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>The user's first and/or last name changed; a null name was removed.</summary>
public sealed record UserPersonNameChangedDomainEvent(PersonName? FirstName, PersonName? LastName) : DomainEvent;
