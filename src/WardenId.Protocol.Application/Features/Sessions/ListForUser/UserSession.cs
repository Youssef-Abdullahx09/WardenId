using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Features.Sessions.ListForUser;

/// <param name="EndedAt">Null while the session has not been ended (it may still have expired).</param>
public sealed record UserSession(
    Guid Sid,
    DateTimeOffset AuthTime,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> ClientIds,
    DateTimeOffset? EndedAt = null,
    EndReason? EndReason = null);
