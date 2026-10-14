using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain.Events;

/// <summary>
/// Raised once per session end. Carries the authorizations so the CAP subscriber can revoke and fan out
/// back-channel logout without reloading the session.
/// </summary>
public sealed record SessionEndedDomainEvent(
    Guid Sid,
    Guid TenantId,
    Guid SubjectId,
    EndReason Reason,
    IReadOnlyList<SessionAuthorizationSnapshot> Authorizations) : DomainEvent;
