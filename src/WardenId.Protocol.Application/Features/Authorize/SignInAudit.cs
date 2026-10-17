using WardenId.Audit.Integration.Command.Features.Events.Record;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Application.Features.Authorize;

internal static class SignInAudit
{
    /// <summary>An attempt against no known account is anonymous: the typed identifier is never recorded.</summary>
    public static RecordAuditEventCommand Failed(Guid tenantId, Guid? userId, OutcomeReason reason, DateTimeOffset occurredAt) =>
        new(
            SecurityEvent.SignInFailed,
            tenantId,
            occurredAt,
            userId is { } actorId ? Actor.User(actorId) : Actor.Anonymous,
            userId is { } targetId ? [Target.User(targetId)] : [],
            reason);
}
