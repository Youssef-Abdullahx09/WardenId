using WardenId.Protocol.Application.Auditing;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeAuthAuditTrail : IAuthAuditTrail
{
    public List<RecordAuditEventCommand> Recorded { get; } = [];

    public Task RecordAsync(RecordAuditEventCommand auditEvent)
    {
        Recorded.Add(auditEvent);
        return Task.CompletedTask;
    }
}
