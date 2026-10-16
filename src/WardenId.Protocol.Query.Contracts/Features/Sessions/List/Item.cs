namespace WardenId.Protocol.Query.Contracts.Features.Sessions.List;

/// <summary>
/// One row of Features/Sessions/List/Query.sql. Times are UTC <see cref="DateTime"/>: Npgsql reads
/// timestamptz as DateTime (Kind=Utc), which Dapper maps directly.
/// </summary>
public sealed class Item
{
    public Guid Sid { get; init; }
    public Guid UserId { get; init; }
    public DateTime AuthTime { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? EndedAt { get; init; }

    /// <summary>The <c>EndReason</c> member name, as persisted.</summary>
    public string? EndReason { get; init; }

    public string[] ClientIds { get; init; } = [];
}
