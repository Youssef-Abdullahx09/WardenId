namespace WardenId.Protocol.Application.Features.BackchannelLogout;

/// <summary>
/// One pending back-channel logout notification: a single client of one ended session. It is queued per
/// client (spec D8), so a failing RP is retried alone instead of re-notifying every participant.
/// </summary>
public sealed record BackchannelLogoutRequested(Guid TenantId, string ClientId, Guid Sid, Guid SubjectId)
{
    public const string Topic = nameof(BackchannelLogoutRequested);
}
