using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Presentation.Tests.Fakes;

/// <summary>Stands in for the Audit module: every audit event Protocol sends, in order.</summary>
public sealed class FakeAuditLog
{
    private readonly Lock _gate = new();
    private readonly List<RecordAuditEventCommand> _events = [];

    public IReadOnlyList<RecordAuditEventCommand> Events
    {
        get
        {
            lock (_gate) return _events.ToArray();
        }
    }

    public void Add(RecordAuditEventCommand auditEvent)
    {
        lock (_gate) _events.Add(auditEvent);
    }
}
