using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Features.Sessions.List;

/// <param name="UserId">The session's subject.</param>
/// <param name="ClientIds">Public client_ids that received an authorization under the session.</param>
public sealed record SessionListItem(
    Guid Sid,
    Guid UserId,
    DateTimeOffset AuthTime,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EndedAt,
    EndReason? EndReason,
    IReadOnlyList<string> ClientIds);
