namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.List;

/// <summary>One row of Features/Clients/List/Query.sql. <c>CreatedAt</c> is a UTC <see cref="DateTime"/> (Npgsql reads timestamptz as Kind=Utc).</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The <c>ClientType</c> member name, as persisted.</summary>
    public string ClientType { get; init; } = string.Empty;

    public bool Enabled { get; init; }
    public bool IsFirstParty { get; init; }
    public DateTime CreatedAt { get; init; }
}
