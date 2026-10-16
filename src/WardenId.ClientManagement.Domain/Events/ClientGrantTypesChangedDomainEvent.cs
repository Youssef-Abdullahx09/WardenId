using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

/// <summary>
/// Raised when a client's allowed grant types change. Protocol reads grants per lookup through
/// ClientManagement.Integration.Command (no cache), so the change applies to the next token request.
/// </summary>
public sealed record ClientGrantTypesChangedDomainEvent(GrantType Previous, GrantType Current) : DomainEvent;
