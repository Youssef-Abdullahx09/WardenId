namespace WardenId.Audit.Infrastructure.Persistence;

/// <summary>The persisted shape of one signed checkpoint — audit.checkpoints.</summary>
public sealed class AuditCheckpointRecord
{
    public Guid Id { get; set; }
    public Guid SegmentId { get; set; }
    public string Channel { get; set; }
    public Guid? TenantId { get; set; }
    public long TreeSize { get; set; }
    public byte[] RootHash { get; set; }
    public string Kid { get; set; }
    public byte[] Signature { get; set; }
    public DateTimeOffset SignedAt { get; set; }
}
