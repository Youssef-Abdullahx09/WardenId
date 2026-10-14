namespace WardenId.Protocol.Application.Features.Sessions.ListForUser;

public sealed record UserSession(
    Guid Sid,
    DateTimeOffset AuthTime,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> ClientIds);
