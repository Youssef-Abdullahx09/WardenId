namespace WardenId.Audit.Application.Tests.Fakes;

/// <summary>In-memory stand-in for AuditProofRepository — tests seed the lists directly, no EF involved.</summary>
public sealed class FakeAuditProofRepository : IAuditProofRepository
{
    public List<AuditEventRow> Events { get; } = [];
    public List<SegmentRow> Segments { get; } = [];
    public List<CheckpointRow> Checkpoints { get; } = [];
    public List<CheckpointKeyRow> Keys { get; } = [];

    public Task<AuditEventRow?> FindEventAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Events.FirstOrDefault(e => e.Id == eventId));

    public Task<SegmentRow?> FindCoveringSegmentAsync(
        AuditChannel channel, Guid? tenantId, long sequence, CancellationToken cancellationToken = default) =>
        Task.FromResult(Segments.FirstOrDefault(s => s.SequenceStart <= sequence && sequence <= s.SequenceEnd));

    public Task<IReadOnlyList<byte[]>> GetSegmentLeafHashesAsync(
        AuditChannel channel, Guid? tenantId, long sequenceStart, long sequenceEnd, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<byte[]> hashes = Events
            .Where(e => e.Sequence is not null && e.Sequence >= sequenceStart && e.Sequence <= sequenceEnd)
            .OrderBy(e => e.Sequence)
            .Select(e => e.LeafHash)
            .ToList();
        return Task.FromResult(hashes);
    }

    public Task<IReadOnlyList<SegmentRow>> GetSegmentsAsync(
        AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SegmentRow> ordered = Segments.OrderBy(s => s.SequenceStart).ToList();
        return Task.FromResult(ordered);
    }

    public Task<IReadOnlyList<CheckpointRow>> GetCheckpointsAsync(
        AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CheckpointRow> ordered = Checkpoints.OrderBy(c => c.TreeSize).ToList();
        return Task.FromResult(ordered);
    }

    public Task<IReadOnlyList<CheckpointKeyRow>> GetCheckpointKeysAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CheckpointKeyRow> keys = Keys.ToList();
        return Task.FromResult(keys);
    }
}
