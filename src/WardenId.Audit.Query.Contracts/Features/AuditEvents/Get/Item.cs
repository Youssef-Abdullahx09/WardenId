namespace WardenId.Audit.Query.Contracts.Features.AuditEvents.Get;

/// <summary>
/// One row of Features/AuditEvents/Get/Query.sql. <see cref="OccurredAt"/> is a UTC <see cref="DateTime"/>: Npgsql reads
/// timestamptz as DateTime (Kind=Utc), which Dapper maps directly. <see cref="Payload"/> is null once retention purged it;
/// the search columns are null (<see cref="Targets"/> empty) where they couldn't be filled.
/// </summary>
public sealed class Item
{
    public Guid Id { get; init; }

    /// <summary>The <c>AuditChannel</c> member name, as persisted.</summary>
    public string Channel { get; init; } = "";

    public Guid? TenantId { get; init; }
    public long? Sequence { get; init; }
    public DateTime OccurredAt { get; init; }
    public byte[]? Payload { get; init; }
    public string? EventType { get; init; }
    public string? Actor { get; init; }
    public string[] Targets { get; init; } = [];

    /// <summary>The <c>AuditResult</c> member name, as persisted.</summary>
    public string? Outcome { get; init; }
}
