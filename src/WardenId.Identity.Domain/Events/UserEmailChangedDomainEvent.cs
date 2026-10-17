using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

/// <summary>The user's email changed; <see cref="Email"/> is unverified until VerifyEmail succeeds.</summary>
public sealed record UserEmailChangedDomainEvent(Email PreviousEmail, Email Email) : DomainEvent;
