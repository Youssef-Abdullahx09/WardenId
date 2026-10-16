using WardenId.Audit.Application;

namespace WardenId.Audit.Application.Tests.Fakes;

public sealed class FakeAuditRecorder : IAuditRecorder
{
    public List<(AuditEvent Event, Guid? TenantId)> Recorded { get; } = [];

    public Exception? Throws { get; set; }

    public Task RecordAsync(AuditEvent auditEvent, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        if (Throws is not null) throw Throws;
        Recorded.Add((auditEvent, tenantId));
        return Task.CompletedTask;
    }
}
