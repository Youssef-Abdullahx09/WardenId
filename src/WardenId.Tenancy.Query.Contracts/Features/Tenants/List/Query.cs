namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.List;

/// <summary>One page of tenants, newest <c>CreatedAt</c> first.</summary>
/// <param name="Status">Only tenants in this <c>Status</c> (member name, as persisted); null for any.</param>
public sealed record Query(
    int PageNumber,
    int PageSize,
    string? Status);
