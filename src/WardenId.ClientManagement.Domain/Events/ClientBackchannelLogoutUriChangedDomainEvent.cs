using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

/// <summary>Raised when a client's back-channel logout URI is set, changed or cleared (<paramref name="Uri"/> null).</summary>
public sealed record ClientBackchannelLogoutUriChangedDomainEvent(string? Uri) : DomainEvent;
