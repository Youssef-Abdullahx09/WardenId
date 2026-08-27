using WardenId.Audit.Infrastructure.Exporting;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

public sealed class FakeAuditSegmentPublisher : IAuditSegmentPublisher
{
    public List<AuditSegmentExportedMessage> Published { get; } = [];
    public bool ThrowOnPublish { get; set; }

    public Task PublishAsync(AuditDbContext db, AuditSegmentExportedMessage message, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish) throw new InvalidOperationException("Simulated publish failure.");

        Published.Add(message);
        return Task.CompletedTask;
    }
}
