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

    /// <summary>Persisted as the member name; Dapper parses it. Null while the session is live.</summary>
    public EndReason? EndReason { get; init; }

    /// <summary>Public client_ids that received an authorization under the session.</summary>
    public string[] ClientIds { get; init; } = [];
}
