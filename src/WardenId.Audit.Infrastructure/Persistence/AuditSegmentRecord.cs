namespace WardenId.Audit.Infrastructure.Persistence;

/// <summary>
/// The persisted shape of one sealed segment — audit.segments. Only AuditDbContext ever
/// touches this table, unlike AuditEventRecord, so — unlike that type — this can live
/// directly in Audit.Infrastructure rather than SharedKernel.Persistence; there's no
/// cross-module mapping need to keep it decoupled from.
/// </summary>
public sealed class AuditSegmentRecord
{
    public Guid Id { get; set; }
    public string Channel { get; set; }
    public Guid? TenantId { get; set; }
    public long SequenceStart { get; set; }
    public long SequenceEnd { get; set; }
    public byte[] MerkleRoot { get; set; }
    public byte[]? PreviousHeaderHash { get; set; }
    public byte[] HeaderHash { get; set; }
    public DateTimeOffset SealedAt { get; set; }

    // Set once this segment's covered events have committed via the phase-5 exporter's CAP
    // publish, in the same transaction as that publish (see AuditExporterService). Retention
    // (docs/audit/OPEN-DECISIONS.md #6) only purges rows whose segment has this set.
    public DateTimeOffset? ExportedAt { get; set; }
}
