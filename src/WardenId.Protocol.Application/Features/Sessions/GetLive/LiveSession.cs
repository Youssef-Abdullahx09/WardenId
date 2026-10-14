namespace WardenId.Protocol.Application.Features.Sessions.GetLive;

public sealed record LiveSession(Guid Sid, Guid UserId, DateTimeOffset AuthTime);
