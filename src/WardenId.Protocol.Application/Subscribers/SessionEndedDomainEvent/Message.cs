using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Subscribers.SessionEndedDomainEvent;

/// <summary>The fields of Protocol's SessionEndedDomainEvent this subscriber reads (CAP JSON, PascalCase).</summary>
public sealed record Message(
    Guid Sid,
    Guid TenantId,
    Guid SubjectId,
    EndReason Reason,
    IReadOnlyList<SessionAuthorizationSnapshot> Authorizations);
