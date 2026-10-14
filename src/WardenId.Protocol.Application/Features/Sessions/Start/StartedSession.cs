namespace WardenId.Protocol.Application.Features.Sessions.Start;

public sealed record StartedSession(Guid Sid, DateTimeOffset AuthTime);
