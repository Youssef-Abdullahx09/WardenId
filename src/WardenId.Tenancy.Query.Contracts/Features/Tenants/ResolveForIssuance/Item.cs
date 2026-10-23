namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.ResolveForIssuance;

/// <summary>One row of Features/Tenants/ResolveForIssuance/Query.sql.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The tenant <c>Status</c> member name, as persisted.</summary>
    public string Status { get; init; } = string.Empty;
}
