namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.List;

/// <summary>One row of Features/Tenants/List/Query.sql. <c>CreatedAt</c> is a UTC <see cref="DateTime"/>.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The tenant <c>Status</c> member name, as persisted.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
