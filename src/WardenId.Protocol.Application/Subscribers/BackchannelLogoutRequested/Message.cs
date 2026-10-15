namespace WardenId.Protocol.Application.Subscribers.BackchannelLogoutRequested;

/// <summary>One queued notification: a single client of one ended session (published by IBackchannelLogoutQueue).</summary>
public sealed record Message(
    Guid TenantId,
    string ClientId,
    Guid Sid,
    Guid SubjectId);
