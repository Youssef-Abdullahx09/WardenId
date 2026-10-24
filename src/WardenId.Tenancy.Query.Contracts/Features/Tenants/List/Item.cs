namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.List;

/// <summary>One row of Features/Tenants/List/Query.sql. <see cref="CreatedAt"/> is a UTC <see cref="DateTime"/>.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Persisted as the member name; Dapper parses it.</summary>
    public Status Status { get; init; }

    public DateTime CreatedAt { get; init; }
}
