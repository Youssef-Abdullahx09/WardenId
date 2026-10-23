namespace WardenId.Protocol.Query.Contracts.Features.Sessions.GetLive;

/// <summary>
/// The row of Features/Sessions/GetLive/Query.sql. Times are UTC <see cref="DateTime"/>: Npgsql reads
/// timestamptz as DateTime (Kind=Utc), which Dapper maps directly.
/// </summary>
public sealed class Item
{
    public Guid Sid { get; init; }
    public Guid UserId { get; init; }
    public DateTime AuthTime { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? EndedAt { get; init; }
}
