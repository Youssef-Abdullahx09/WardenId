namespace WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser;

/// <summary>
/// One row of Features/Sessions/ListForUser/Query.sql. Times are UTC <see cref="DateTime"/>: Npgsql reads
/// timestamptz as DateTime (Kind=Utc), which Dapper maps directly.
/// </summary>
public sealed class Item
{
    public Guid Sid { get; init; }
    public DateTime AuthTime { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string[] ClientIds { get; init; } = [];
}
