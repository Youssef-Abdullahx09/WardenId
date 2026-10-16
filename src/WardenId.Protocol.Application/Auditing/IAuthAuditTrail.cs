using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Application.Auditing;

/// <summary>
/// Protocol's way into the audit trail for sign-in, session and token events. Fail-open (audit spec I10): it
/// never throws and never reports failure, so an audit outage can't break a sign-in or a token request.
/// </summary>
public interface IAuthAuditTrail
{
    Task RecordAsync(RecordAuditEventCommand auditEvent);
}
