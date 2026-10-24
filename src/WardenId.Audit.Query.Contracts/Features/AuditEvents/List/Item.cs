namespace WardenId.Audit.Query.Contracts.Features.AuditEvents.List;

/// <summary>
/// One row of Features/AuditEvents/List/Query.sql. <see cref="OccurredAt"/> is a UTC <see cref="DateTime"/>: Npgsql reads
/// timestamptz as DateTime (Kind=Utc), which Dapper maps directly. <see cref="Payload"/> is null once retention purged it;
/// the search columns are null (<see cref="Targets"/> empty) where they couldn't be filled.
/// </summary>
public sealed class Item
{
    public Guid Id { get; init; }

    /// <summary>Persisted as the member name; Dapper parses it.</summary>
    public AuditChannel Channel { get; init; }

    public Guid? TenantId { get; init; }
    public long? Sequence { get; init; }
    public DateTime OccurredAt { get; init; }
    public byte[]? Payload { get; init; }
    public string? EventType { get; init; }
    public string? Actor { get; init; }
    public string[] Targets { get; init; } = [];

    /// <summary>Persisted as the member name; Dapper parses it.</summary>
    public AuditResult? Outcome { get; init; }
}
