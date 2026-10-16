using MediatR;
using Microsoft.Extensions.Logging;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Application.Auditing;

/// <summary>
/// Sends the event to the Audit module in-process. Swallows every failure (I10, fail-open on authn.*), and sends
/// with no cancellation token: a client that hangs up right after signing in must not drop the audit row.
/// </summary>
public sealed class SenderAuthAuditTrail(ISender sender, ILogger<SenderAuthAuditTrail> logger) : IAuthAuditTrail
{
    public async Task RecordAsync(RecordAuditEventCommand auditEvent)
    {
        try
        {
            var result = await sender.Send(auditEvent, CancellationToken.None);
            if (result.IsFailed)
                logger.LogWarning(
                    "Audit event {Event} for tenant {TenantId} was not recorded: {Errors}",
                    auditEvent.Event, auditEvent.TenantId, string.Join("; ", result.Errors.Select(e => e.Message)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit event {Event} for tenant {TenantId} was not recorded", auditEvent.Event, auditEvent.TenantId);
        }
    }
}
