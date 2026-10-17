using WardenId.Audit.Integration.Command.Features.Events.Record;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Application.Auditing;

/// <summary>
/// The audit events of a user's own account changes and of the checks in front of them: actor and target are the
/// user. Nothing the user typed (password, code, recovery code) and nothing issued (recovery codes, secrets) is
/// ever part of them.
/// </summary>
internal static class UserChangeAudit
{
    /// <param name="sid">The login session the change was made from (account pages); null during sign-in.</param>
    public static RecordAuditEventCommand Changed(
        SecurityEvent change, Guid tenantId, Guid userId, DateTimeOffset occurredAt, Guid? sid = null) =>
        new(change, tenantId, occurredAt, Actor.User(userId), [Target.User(userId)], Session: SessionOf(sid));

    public static RecordAuditEventCommand StepUpFailed(
        Guid tenantId, Guid userId, OutcomeReason reason, DateTimeOffset occurredAt, Guid sid) =>
        new(SecurityEvent.StepUpFailed, tenantId, occurredAt, Actor.User(userId), [Target.User(userId)], reason,
            SessionOf(sid));

    private static SessionContext? SessionOf(Guid? sid) => sid is { } id ? new SessionContext(id) : null;
}
